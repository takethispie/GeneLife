using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Government-funded unemployment benefits, voluntary resignation after wage
/// arrears, and post-benefit salary decay. Benefits are never minted.
/// </summary>
public class UnemploymentBenefitSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription unemployedQuery;
    private readonly QueryDescription arrearsQuery;
    private readonly int interval;

    public UnemploymentBenefitSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerPayCycle;
        unemployedQuery = new QueryDescription()
            .WithAll<Lifecycle, JobSeeker>()
            .WithNone<Employee, Dead>();
        arrearsQuery = new QueryDescription()
            .WithAll<Employee, WageClaim, Lifecycle>()
            .WithNone<Dead>();
    }

    public string Name => "UnemploymentBenefit";
    public int Order => 525;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var events = new List<object>();
        ResignFromArrears(tick, events);
        PayBenefits(tick, events);

        foreach (var @event in events)
            economy.Emit(@event);
    }

    private void ResignFromArrears(long tick, List<object> events)
    {
        var resignations = new List<(Entity Entity, Employee Employee, SkillProfile Profile)>();
        world.Query(in arrearsQuery, (Entity entity, ref Employee employee, ref WageClaim claim,
            ref Lifecycle lifecycle) =>
        {
            if (lifecycle.Stage != LifeStage.Work || claim.CyclesInArrears < 2)
                return;
            var profile = world.Has<SkillProfile>(entity) ? world.Get<SkillProfile>(entity) : new SkillProfile();
            resignations.Add((entity, employee, profile));
        });

        foreach (var (entity, employee, profile) in resignations)
        {
            if (!world.IsAlive(entity) || !world.Has<Employee>(entity))
                continue;

            var salary = employee.SalaryPerCycle;
            LifeSystemHelpers.RemoveEmployment(world, economy.Employment, economy, entity, tick);
            LifeSystemHelpers.EnsureWallet(world, entity);

            if (!world.Has<JobSeeker>(entity))
            {
                var careers = economy.Life.Careers;
                var desired = salary.Multiply(0.9m).Max(careers.MinimumSalary);
                world.Add(entity, new JobSeeker(true, profile.ToMask(), desired));
            }

            var record = CreateRecord(entity, salary, tick);
            world.Add(entity, record);
            economy.Emit(new UnemploymentStarted(
                LifeSystemHelpers.NetworkIdOf(world, entity),
                record.BenefitPerCycle.Cents,
                record.MaxCycles,
                record.Eligible,
                tick));
        }
    }

    private void PayBenefits(long tick, List<object> events)
    {
        var missingRecords = new List<(Entity Entity, JobSeeker Seeker)>();
        world.Query(in unemployedQuery, (Entity entity, ref Lifecycle lifecycle, ref JobSeeker seeker) =>
        {
            if (lifecycle.Stage != LifeStage.Work)
                return;
            if (world.Has<UnemploymentRecord>(entity))
                return;
            missingRecords.Add((entity, seeker));
        });

        foreach (var (entity, seeker) in missingRecords)
        {
            if (!world.IsAlive(entity) || world.Has<UnemploymentRecord>(entity))
                continue;

            var record = CreateRecord(entity, seeker.DesiredSalary, tick);
            world.Add(entity, record);
            events.Add(new UnemploymentStarted(
                LifeSystemHelpers.NetworkIdOf(world, entity),
                record.BenefitPerCycle.Cents,
                record.MaxCycles,
                record.Eligible,
                tick));
        }

        var updated = new List<(Entity Entity, UnemploymentRecord Record, JobSeeker Seeker)>();
        var walletCredits = new List<(Entity Entity, Money Amount)>();
        var query = new QueryDescription()
            .WithAll<Lifecycle, JobSeeker, UnemploymentRecord>()
            .WithNone<Employee, Dead>();

        world.Query(in query, (Entity entity, ref Lifecycle lifecycle, ref JobSeeker seeker,
            ref UnemploymentRecord record) =>
        {
            if (lifecycle.Stage != LifeStage.Work || record.LastPaidTick == tick)
                return;

            if (record.Eligible && !record.IsExhausted && record.BenefitPerCycle.IsPositive)
            {
                var paid = record.BenefitPerCycle.Min(economy.Government.Balance);
                if (paid.IsPositive)
                {
                    economy.Government.Balance -= paid;
                    economy.Government.BenefitsPaid += paid;
                    if (world.Has<Wallet>(entity))
                        world.Get<Wallet>(entity).Balance += paid;
                    else
                        walletCredits.Add((entity, paid));
                    economy.Ledger.Record(
                        Guid.NewGuid(),
                        tick,
                        LedgerKind.UnemploymentBenefit,
                        EconomyAccountIds.Government,
                        LifeSystemHelpers.NetworkIdOf(world, entity),
                        paid,
                        "unemployment.benefit");
                    events.Add(new BenefitPaid(
                        LifeSystemHelpers.NetworkIdOf(world, entity),
                        paid.Cents,
                        record.CyclesClaimed + 1,
                        record.MaxCycles,
                        tick));
                }
                else
                {
                    record.UnpaidCycles++;
                }
            }

            record.CyclesClaimed++;
            record.LastPaidTick = tick;

            if (record.IsExhausted)
            {
                events.Add(new BenefitExhausted(LifeSystemHelpers.NetworkIdOf(world, entity), tick));

                var floor = economy.Life.Careers.MinimumSalary.Multiply(0.6m);
                var desired = seeker.DesiredSalary.Multiply(0.9m).Max(floor);
                if (desired != seeker.DesiredSalary)
                {
                    seeker.DesiredSalary = desired;
                    if (world.Has<JobSeeker>(entity))
                        world.Get<JobSeeker>(entity) = seeker;
                }
            }

            updated.Add((entity, record, seeker));
        });

        foreach (var (entity, amount) in walletCredits)
        {
            if (!world.IsAlive(entity) || world.Has<Wallet>(entity))
                continue;
            world.Add(entity, new Wallet(amount));
        }

        foreach (var (entity, record, seeker) in updated)
        {
            if (!world.IsAlive(entity))
                continue;
            if (world.Has<UnemploymentRecord>(entity))
                world.Get<UnemploymentRecord>(entity) = record;
            if (world.Has<JobSeeker>(entity))
                world.Get<JobSeeker>(entity) = seeker;
        }
    }

    private UnemploymentRecord CreateRecord(Entity entity, Money lastSalary, long tick)
    {
        var eligible = world.Has<PensionAccount>(entity) &&
                       world.Get<PensionAccount>(entity).ContributingCycles >=
                       economy.Life.Pensions.MinimumContributingCycles;

        var maxCycles = 6;
        var household = LifeSystemHelpers.HouseholdOf(world, economy, entity);
        if (household != Entity.Null)
        {
            var children = 0;
            foreach (var member in economy.Housing.GetMembers(household))
            {
                if (world.IsAlive(member) && world.Has<HouseholdMember>(member) &&
                    world.Get<HouseholdMember>(member).Role == HouseholdRole.Child)
                    children++;
            }
            maxCycles = Math.Min(10, maxCycles + children * 2);
        }

        var benefit = lastSalary.Multiply(0.4m);
        return new UnemploymentRecord(tick, benefit, maxCycles, eligible);
    }
}

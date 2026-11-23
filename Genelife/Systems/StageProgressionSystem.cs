using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Life;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Owns every lifecycle transition. Collects changes while iterating, then
/// applies them with idempotent enter/exit handlers. Runs before HiringSystem so
/// a graduate can be hired on the same tick.
/// </summary>
public class StageProgressionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public StageProgressionSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? 20;
        query = new QueryDescription().WithAll<Lifecycle, Alive>().WithNone<Dead>();
    }

    public string Name => "StageProgression";
    public int Order => 390;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick, context.Time);

    public void Update(long tick) => Update(tick, economy.Clock);

    public void Update(long tick, SimulationClock clock)
    {
        var policy = economy.Life.Policy;
        var changes = new List<(Entity Entity, Lifecycle Lifecycle, Alive Alive, LifeStage Target)>();

        world.Query(in query, (Entity entity, ref Lifecycle lifecycle, ref Alive alive) =>
        {
            var target = policy.StageFor(alive.Age);
            if (target != lifecycle.Stage)
                changes.Add((entity, lifecycle, alive, target));
        });

        foreach (var change in changes)
        {
            if (!world.IsAlive(change.Entity))
                continue;

            var lifecycle = change.Lifecycle;
            while (lifecycle.Stage != change.Target)
            {
                var from = lifecycle.Stage;
                var to = (int)change.Target > (int)from
                    ? policy.Next(from)
                    : change.Target;

                ApplyEnter(change.Entity, to, tick);
                lifecycle.Stage = to;
                lifecycle.StageEnteredTick = tick;
                lifecycle.LastCause = policy.CauseFor(to);
                economy.Life.Index.OnStageChanged(from, to);

                economy.Emit(new LifeStageChanged(
                    LifeSystemHelpers.NetworkIdOf(world, change.Entity),
                    from.ToString(),
                    to.ToString(),
                    lifecycle.LastCause.ToString(),
                    tick));
            }

            world.Get<Lifecycle>(change.Entity) = lifecycle;
        }
    }

    private void ApplyEnter(Entity sim, LifeStage stage, long tick)
    {
        switch (stage)
        {
            case LifeStage.Work:
                EnterWork(sim, tick);
                break;
            case LifeStage.Retirement:
                EnterRetirement(sim, tick);
                break;
            case LifeStage.Elderly:
                EnterElderly(sim, tick);
                break;
            case LifeStage.Education:
            default:
                LifeSystemHelpers.RemoveEmployment(world, economy.Employment, economy, sim, tick);
                LifeSystemHelpers.RemoveJobSearch(world, sim);
                if (world.Has<RetirementIncome>(sim))
                    world.Remove<RetirementIncome>(sim);
                world.SetOrAdd(sim, new Health(world.Has<Health>(sim) ? world.Get<Health>(sim).Value : 100f));
                break;
        }
    }

    private void EnterWork(Entity sim, long tick)
    {
        ChildhoodPolicy.ClearDependentMarker(world, sim);
        EnrollmentHelpers.ReleaseSeat(world, sim, economy.Life.Index);

        if (world.Has<RetirementIncome>(sim))
            world.Remove<RetirementIncome>(sim);

        LifeSystemHelpers.EnsureWallet(world, sim);

        if (world.Has<Employee>(sim) || world.Has<JobSeeker>(sim))
            return;

        var profile = world.Has<SkillProfile>(sim) ? world.Get<SkillProfile>(sim) : new SkillProfile();
        if (profile.ToMask() == Skill.None)
        {
            profile.Add(Skill.Sales, 0.3f);
            world.SetOrAdd(sim, profile);
        }
        var salary = economy.Life.Careers.SalaryForSkills(profile.Total);
        var household = LifeSystemHelpers.HouseholdOf(world, economy, sim);
        if (household != Entity.Null)
        {
            var members = economy.Housing.MemberCount(household);
            var earners = CountEarners(household);
            var dependents = Math.Max(0, members - earners);
            var pressure = 1m + 0.1m * (dependents / (decimal)Math.Max(1, earners));
            salary = salary.Multiply(pressure);
        }

        world.Add(sim, new JobSeeker(true, profile.ToMask(), salary));
        economy.Life.GraduatesThisYear++;
        economy.Emit(new StudentGraduated(
            LifeSystemHelpers.NetworkIdOf(world, sim),
            (int)profile.ToMask(),
            salary.Cents,
            tick));

        if (household != Entity.Null)
            economy.Emit(new HouseholdIncomeChanged(
                LifeSystemHelpers.NetworkIdOf(world, household),
                tick));
    }

    private void EnterRetirement(Entity sim, long tick)
    {
        LifeSystemHelpers.RemoveEmployment(world, economy.Employment, economy, sim, tick, retire: true);
        LifeSystemHelpers.RemoveJobSearch(world, sim);
        if (world.Has<UnemploymentRecord>(sim))
            world.Remove<UnemploymentRecord>(sim);

        var account = world.Has<PensionAccount>(sim) ? world.Get<PensionAccount>(sim) : default;
        var (pension, meansTested) = economy.Life.Pensions.EntitlementFor(account, economy.Life.Policy);

        if (!world.Has<RetirementIncome>(sim))
            world.Add(sim, new RetirementIncome(pension, tick, meansTested ? "means-tested" : "statement", meansTested));

        economy.Life.RetiredThisYear++;
        var household = LifeSystemHelpers.HouseholdOf(world, economy, sim);
        if (household != Entity.Null)
            economy.Emit(new HouseholdIncomeChanged(LifeSystemHelpers.NetworkIdOf(world, household), tick));

        economy.Emit(new Retired(
            LifeSystemHelpers.NetworkIdOf(world, sim),
            world.Has<Alive>(sim) ? world.Get<Alive>(sim).Age : economy.Life.Policy.RetirementAge,
            pension.Cents,
            tick));
    }

    private void EnterElderly(Entity sim, long tick)
    {
        if (!world.Has<Health>(sim))
            world.Add(sim, new Health(60f));
        LifeSystemHelpers.RemoveJobSearch(world, sim);
    }

    private int CountEarners(Entity household)
    {
        var count = 0;
        foreach (var member in economy.Housing.GetMembers(household))
        {
            if (world.IsAlive(member) && world.Has<Employee>(member))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Admin scenario helper: sets the age to the stage boundary so the next
    /// progression pass applies the full transition with all handlers.
    /// </summary>
    public bool ForceStage(ulong simNetworkId, LifeStage stage, long tick)
    {
        var queryInWorld = new QueryDescription().WithAll<NetworkId, Alive>();
        var found = false;
        Entity foundEntity = Entity.Null;

        world.Query(in queryInWorld, (Entity entity, ref NetworkId networkId, ref Alive alive) =>
        {
            if (found || networkId.Value != simNetworkId)
                return;
            found = true;
            foundEntity = entity;
            alive.Age = economy.Life.Policy.EntryAge(stage);
        });

        if (!found || !world.IsAlive(foundEntity))
            return false;

        var age = world.Get<Alive>(foundEntity).Age;
        var ageTicks = (long)age * economy.Clock.TicksPerYear;
        if (world.Has<Lifecycle>(foundEntity))
        {
            var lifecycle = world.Get<Lifecycle>(foundEntity);
            lifecycle.AgeTicks = ageTicks;
            lifecycle.LastAgedTick = tick;
            world.Get<Lifecycle>(foundEntity) = lifecycle;
        }
        else
        {
            world.Add(foundEntity, LifecycleFactory.Create(age, tick, economy.Clock, economy.Life.Policy));
            economy.Life.Index.OnBorn(stage);
        }

        return true;
    }
}

internal static class WorldLifeExtensions
{
    public static void SetOrAdd<T>(this World world, Entity entity, T component) where T : struct
    {
        if (world.Has<T>(entity))
            world.Get<T>(entity) = component;
        else
            world.Add(entity, component);
    }
}

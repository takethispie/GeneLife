using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Yearly career progression: promotions, demotions, salary indexation and
/// backfill signals. Promotions move at most one rung per review.
/// </summary>
public class CareerSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public CareerSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerYear;
        query = new QueryDescription()
            .WithAll<Employee, Lifecycle, EmploymentRecord>()
            .WithNone<Dead>();
    }

    public string Name => "Career";
    public int Order => 350;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var events = new List<object>();
        var policy = economy.Life.Careers;

        world.Query(in query, (Entity entity, ref Employee employee, ref Lifecycle lifecycle,
            ref EmploymentRecord record) =>
        {
            if (lifecycle.Stage != LifeStage.Work)
                return;

            var profile = world.Has<SkillProfile>(entity) ? world.Get<SkillProfile>(entity) : new SkillProfile();
            var skillFit = Math.Clamp(profile.Total / 2.5f, 0f, 1f);
            var tenureFactor = Math.Clamp(record.TenureCycles / 8f, 0f, 1f);
            var healthFactor = CompanyHealth(employee.Company);

            var score = 0.45f * record.Performance
                        + 0.25f * tenureFactor
                        + 0.20f * skillFit
                        + 0.10f * healthFactor;

            var previousSalary = employee.SalaryPerCycle;
            var changed = false;

            if (score >= policy.PromotionThreshold &&
                employee.Seniority < Seniority.Director &&
                record.HighPerformanceMonths >= 6)
            {
                var from = employee.Seniority;
                employee.Seniority = policy.Next(from);
                employee.SalaryPerCycle = employee.SalaryPerCycle
                    .Multiply(1m + policy.PromotionRaise)
                    .Min(policy.MaximumSalary);
                record.HighPerformanceMonths = 0;
                changed = true;
                events.Add(new PromotionGranted(
                    LifeSystemHelpers.NetworkIdOf(world, entity),
                    from.ToString(),
                    employee.Seniority.ToString(),
                    employee.SalaryPerCycle.Cents,
                    tick));
            }
            else if (score <= policy.DemotionThreshold && record.Warnings >= 3)
            {
                var from = employee.Seniority;
                employee.Seniority = policy.Previous(from);
                employee.SalaryPerCycle = employee.SalaryPerCycle
                    .Multiply(1m - policy.DemotionCut)
                    .Max(policy.MinimumSalary);
                record.Warnings = 0;
                changed = true;
                events.Add(new DemotionApplied(
                    LifeSystemHelpers.NetworkIdOf(world, entity),
                    from.ToString(),
                    employee.Seniority.ToString(),
                    employee.SalaryPerCycle.Cents,
                    tick));
            }
            else if (healthFactor >= 1f && policy.AnnualIndexation > 0m)
            {
                var indexed = employee.SalaryPerCycle.Multiply(1m + policy.AnnualIndexation);
                if (indexed != employee.SalaryPerCycle)
                {
                    employee.SalaryPerCycle = indexed;
                    changed = true;
                }
            }

            if (changed)
            {
                world.Get<Employee>(entity) = employee;
                if (previousSalary != employee.SalaryPerCycle)
                    events.Add(new SalaryAdjusted(
                        LifeSystemHelpers.NetworkIdOf(world, entity),
                        previousSalary.Cents,
                        employee.SalaryPerCycle.Cents,
                        "annual.review",
                        tick));
            }

            world.Get<EmploymentRecord>(entity) = record;
        });

        foreach (var @event in events)
            economy.Emit(@event);
    }

    private float CompanyHealth(Entity company)
    {
        if (!world.IsAlive(company) || !world.Has<CompanyAccount>(company))
            return 0.8f;

        var account = world.Get<CompanyAccount>(company);
        return account.State switch
        {
            CompanySolvency.Healthy => 1f,
            CompanySolvency.Tight => 0.5f,
            CompanySolvency.Arrears => 0.25f,
            _ => 0f
        };
    }
}

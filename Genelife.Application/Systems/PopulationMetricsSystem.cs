using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Life;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Yearly population snapshot and balance checks. Aggregation is O(genoses) once
/// per simulated year and never per tick.
/// </summary>
public class PopulationMetricsSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    private readonly RelationshipIndex? relationships;

    public PopulationMetricsSystem(
        World world,
        EconomyContext economy,
        int? interval = null,
        RelationshipIndex? relationships = null)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.interval = interval ?? economy.Clock.TicksPerYear;
    }

    public string Name => "PopulationMetrics";
    public int Order => 620;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick, context.Time);

    public void Update(long tick) => Update(tick, economy.Clock);

    public void Update(long tick, SimulationClock clock)
    {
        var life = economy.Life;
        var ages = new List<int>();
        var healthTotal = 0f;
        var healthCount = 0;
        var stage = new int[4];
        var childEntities = new List<Entity>();

        world.Query(in new QueryDescription().WithAll<Lifecycle, Alive>().WithNone<Dead>(),
            (Entity entity, ref Lifecycle lifecycle, ref Alive alive) =>
            {
                stage[(int)lifecycle.Stage]++;
                ages.Add(alive.Age);
                if (alive.Age < economy.Life.Policy.WorkStartAge)
                    childEntities.Add(entity);
                if (world.Has<Health>(entity))
                {
                    healthTotal += world.Get<Health>(entity).Value;
                    healthCount++;
                }
            });

        var total = ages.Count;
        ages.Sort();
        var averageAge = total > 0 ? (decimal)ages.Sum() / total : 0m;
        var medianAge = Median(ages);
        var averageHealth = healthCount > 0 ? (decimal)healthTotal / healthCount : 0m;

        var employed = 0;
        world.Query(in new QueryDescription().WithAll<Employee>().WithNone<Dead>(),
            (ref Employee _) => employed++);

        var unemployed = 0;
        world.Query(in new QueryDescription().WithAll<Lifecycle, JobSeeker>().WithNone<Employee, Dead>(),
            (ref Lifecycle lifecycle) =>
            {
                if (lifecycle.Stage == LifeStage.Work)
                    unemployed++;
            });

        var pensionTotal = Money.Zero;
        var pensionCount = 0;
        world.Query(in new QueryDescription().WithAll<RetirementIncome>().WithNone<Dead>(),
            (ref RetirementIncome income) =>
            {
                pensionTotal += income.MonthlyPension;
                pensionCount++;
            });

        var monthsPerCycle = Math.Max(1, clock.TicksPerPayCycle / clock.TicksPerMonth);
        var monthlyBase = economy.Life.Careers.BaseSalary.Cents / (decimal)monthsPerCycle;
        var averagePension = pensionCount > 0 ? pensionTotal.Cents / (decimal)pensionCount : 0m;
        var replacementRate = monthlyBase > 0 ? averagePension / monthlyBase : 0m;
        var fundingRatio = life.PensionFundingRatio(economy.Pension.Balance);

        var agesAtDeath = life.MortalityLog.Select(record => record.Age).ToList();
        var averageAgeAtDeath = agesAtDeath.Count > 0 ? (decimal)agesAtDeath.Average() : 0m;
        var lifeExpectancy = averageAgeAtDeath > 0 ? averageAgeAtDeath : averageAge;

        var workingAge = stage[(int)LifeStage.Work];
        var oldAge = stage[(int)LifeStage.Retirement] + stage[(int)LifeStage.Elderly];
        var workingShare = total > 0 ? workingAge / (decimal)total : 0m;
        var dependency = workingAge > 0 ? oldAge / (decimal)workingAge : 0m;

        var buckets = new int[9];
        foreach (var age in ages)
            buckets[Bucket(age)]++;

        var causes = life.MortalityLog
            .GroupBy(record => record.Cause)
            .Select(group => new DeathCauseCount(group.Key.ToString(), group.Count()))
            .ToList();

        var births = life.BirthsThisYear;
        var deaths = life.DeathsThisYear;
        var arrivals = life.ArrivalsThisYear;
        var children = ages.Count(age => age < 18);
        var underSchool = ages.Count(age => age < economy.Life.Policy.SchoolStartAge);
        var households = 0;
        world.Query(in new QueryDescription().WithAll<Household>(), (ref Household _) => households++);
        var averageHouseholdSize = households > 0 ? total / (decimal)households : 0m;
        var stateCare = 0;
        world.Query(in new QueryDescription().WithAll<StateCare>(), (ref StateCare _) => stateCare++);
        var orphans = 0;
        if (relationships is not null)
        {
            foreach (var child in childEntities)
            {
                var hasParent = relationships.ParentsOf(child)
                    .Any(parent => world.IsAlive(parent) && !world.Has<Dead>(parent));
                var hasGuardian = relationships.GuardiansOf(child)
                    .Any(guardian => world.IsAlive(guardian) && !world.Has<Dead>(guardian));
                if (!hasParent && !hasGuardian && !world.Has<StateCare>(child))
                    orphans++;
            }
        }
        var guardianCoverage = children > 0 ? (children - orphans) / (decimal)children : 1m;
        var trustTotal = Money.Zero;
        world.Query(in new QueryDescription().WithAll<TrustAccount>(),
            (ref TrustAccount trust) => trustTotal += trust.Balance);
        var birthRate = total > 0 ? births * 1_000m / total : 0m;
        var populationReplacementRate = deaths > 0 ? births / (decimal)deaths : births > 0 ? 1.5m : 1m;

        var snapshot = new PopulationSnapshot(
            tick,
            clock.YearOf(tick),
            total,
            stage[(int)LifeStage.Education],
            stage[(int)LifeStage.Work],
            stage[(int)LifeStage.Retirement],
            stage[(int)LifeStage.Elderly],
            life.Index.EnrolledStudents,
            life.GraduatesThisYear,
            employed,
            unemployed,
            life.RetiredThisYear,
            life.DeathsThisYear,
            life.ArrivalsThisYear,
            averageAge,
            medianAge,
            averageHealth,
            averageAgeAtDeath,
            lifeExpectancy,
            replacementRate,
            fundingRatio,
            workingShare,
            dependency,
            buckets,
            causes,
            births,
            birthRate,
            births - deaths,
            arrivals,
            populationReplacementRate,
            children,
            underSchool,
            averageHouseholdSize,
            orphans,
            stateCare,
            guardianCoverage,
            trustTotal,
            life.InheritancePaidThisYear,
            life.TrustReleasedThisYear);

        life.MetricHistory.Add(snapshot);
        life.ResetYearCounters();
    }

    private static int Bucket(int age) => age switch
    {
        <= 5 => 0,
        <= 17 => 1,
        <= 24 => 2,
        <= 34 => 3,
        <= 44 => 4,
        <= 54 => 5,
        <= 64 => 6,
        <= 79 => 7,
        _ => 8
    };

    private static decimal Median(List<int> values)
    {
        if (values.Count == 0)
            return 0m;
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2m
            : values[middle];
    }
}

public enum PopulationHealthLevel
{
    Pass,
    Warn,
    Fail
}

public static class PopulationHealth
{
    public static List<PopulationHealthCheck> Evaluate(PopulationSnapshot? snapshot)
    {
        var checks = new List<PopulationHealthCheck>();
        if (snapshot is null)
        {
            checks.Add(new PopulationHealthCheck("snapshot", "Warn", "No population snapshot yet."));
            return checks;
        }

        checks.Add(Low("working-age-share", snapshot.WorkingAgeShare, 0.55m, 0.40m));
        checks.Add(High("old-age-dependency", snapshot.OldAgeDependencyRatio, 0.45m, 0.70m, snapshot.OldAgeDependencyRatio > 0.70m));

        var schoolAge = snapshot.PyramidBuckets.Count > 1 ? snapshot.PyramidBuckets[1] : 0;
        var enrollmentRate = schoolAge > 0 ? snapshot.StudentsEnrolled / (decimal)schoolAge : 1m;
        checks.Add(Low("enrollment-rate", enrollmentRate, 0.90m, 0.75m));

        checks.Add(Low("pension-funding-ratio", snapshot.PensionFundingRatio, 0.80m, 0.50m));
        checks.Add(Low("life-expectancy", snapshot.LifeExpectancy, 65m, 55m));

        return checks;
    }

    private static PopulationHealthCheck High(
        string name, decimal value, decimal warn, decimal fail, bool isFail)
    {
        var level = isFail || value > fail ? PopulationHealthLevel.Fail
            : value > warn ? PopulationHealthLevel.Warn
            : PopulationHealthLevel.Pass;
        return new PopulationHealthCheck(name, level.ToString(), value.ToString("0.00"));
    }

    private static PopulationHealthCheck Low(string name, decimal value, decimal warn, decimal fail)
    {
        var level = value < fail ? PopulationHealthLevel.Fail
            : value < warn ? PopulationHealthLevel.Warn
            : PopulationHealthLevel.Pass;
        return new PopulationHealthCheck(name, level.ToString(), value.ToString("0.00"));
    }
}

using System.Text.Json;
using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Housing;
using Genelife.Life;
using Genelife.Values;

namespace Genelife.UnitTests.Economy;

/// <summary>
/// Reproducible decade-long balance report. The test is a measurement harness:
/// it writes the yearly economy, housing and population snapshots plus a few
/// live entity counts so a balance pass can be compared run to run.
/// </summary>
[Trait("Category", "Scenario")]
public class BalanceScenarioTests
{
    [Theory]
    [InlineData(60, 21)]
    [InlineData(200, 21)]
    [InlineData(1000, 21)]
    [InlineData(2000, 21)]
    public void Ten_year_balance_report(int genoses, int seed)
    {
        using var engine = new SimulationEngine();
        engine.Initialize(new EconomyOptions(), new HousingOptions());

        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world,
                engine.NetworkIds,
                engine.Economy!,
                engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(genoses, seed: seed);
            return 0;
        });

        var startSupply = engine.Economy!.TotalMoneySupply();
        var startSource = engine.Economy.Ledger.SourceTotal;
        var startSink = engine.Economy.Ledger.SinkTotal;
        var startLiving = engine.Economy.Life.Index.TotalLiving;
        var ticks = 10 * engine.Economy.Clock.TicksPerYear;
        for (var i = 0; i < ticks - 1; i++)
            engine.Tick(1f / 20f);

        var flows = new BalanceFlows(
            engine.Economy.WagesPaidThisYear.Cents,
            engine.Economy.RevenueThisYear.Cents,
            engine.Economy.ConsumptionThisYear.Cents,
            engine.Economy.TaxThisYear.Cents,
            engine.Economy.BasicIncomeThisYear.Cents,
            engine.Economy.Housing.Market.RentCollectedThisYear.Cents,
            engine.Economy.Housing.Market.UtilityCollectedThisYear.Cents,
            engine.Economy.Housing.Market.UpkeepPaidThisYear.Cents);

        engine.Tick(1f / 20f);

        var report = BuildReport(engine, genoses, seed, startSupply, startSource, startSink, startLiving, flows);
        var dir = Path.Combine(AppContext.BaseDirectory, "balance-reports");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"balance-{genoses}-{seed}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        Console.WriteLine($"[balance] {path}");

        Assert.Equal(0, report.SinkDeltaCents);
        Assert.Equal(0, report.ConservationGapCents);
        Assert.InRange(report.DecadeMoneySupplyGrowth, 0m, 0.10m);
        Assert.True(report.EndLiving > 0, "population collapsed");

        // Rent burden and Gini are asserted against the TARGET at every scale, never
        // against the measured baseline, which would bake in whatever the current value
        // happens to be. Both were RED from 5ddc300 (the consumption ordering change)
        // until the rent-burden rebalance in ede655b brought them inside the bands.
        AssertBalanceBands(report);

        if (genoses == 200)
            AssertTargetBands(report);
    }

    [Fact]
    public void Seeded_world_places_scaled_companies_and_housing_on_the_map()
    {
        using var engine = new SimulationEngine();
        engine.Initialize(new EconomyOptions(), new HousingOptions());

        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world,
                engine.NetworkIds,
                engine.Economy!,
                engine.EmploymentIndex!,
                engine.MapContext,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(200, seed: 21);
            return 0;
        });

        var companies = 0;
        engine.World!.Query(in new QueryDescription().WithAll<CompanyName>(),
            (ref CompanyName _) => companies++);
        var units = 0;
        engine.World.Query(in new QueryDescription().WithAll<HousingUnit>(),
            (ref HousingUnit _) => units++);

        Assert.True(companies >= 10, $"expected seeded companies, found {companies}");
        Assert.True(units >= 150, $"expected seeded housing, found {units}");
    }

    /// <summary>
    /// The other V5 bands, checked at the reference scale (200). All currently pass.
    /// Rent burden, Gini and wage share live in <see cref="AssertBalanceBands"/>
    /// because they are asserted at every scale.
    /// </summary>
    private static void AssertTargetBands(BalanceReport report)
    {
        Assert.InRange(report.EndLiving, report.StartLiving * 4 / 5, report.StartLiving * 6 / 5);
        Assert.True(report.Employees > 0);

        var final = report.Years[^1];
        Assert.InRange(final.UnemploymentRate, 0m, 0.10m);
        Assert.Equal(0, final.HomelessHouseholds);
        Assert.InRange(final.PensionFundingRatio, 0.80m, 2m);
        Assert.InRange(final.HousingVacancyRate, 0m, 0.12m);
    }

    /// <summary>
    /// Bands asserted at EVERY scale: rent burden (≤ 0.40), Gini (≤ 0.70) and
    /// wage share of firm revenue (≤ 1.00). The wage-share ceiling is the
    /// closed-loop non-deficit bound, not the ILO labour income share (~52 % of
    /// value added) — GeneLife models no intermediate inputs and no separate
    /// capital factor, so its labour share legitimately runs high and crosses 1
    /// when firms collectively pay out more wages than the revenue they take in.
    /// Indexing the consumer budget with nominal wages (demand recycling) keeps
    /// it interior; before that fix it reached 1.103 at 60 genoses.
    ///
    /// Do NOT relax a ceiling to the measured baseline: a ratchet set just above
    /// the current value hides a live regression rather than reporting it. A red
    /// band is a work item — never annotate it as expected.
    /// </summary>
    private static void AssertBalanceBands(BalanceReport report)
    {
        var final = report.Years[^1];

        Assert.True(final.MedianRentBurden <= 0.40m,
            $"{report.RequestedGenoses}-{report.Seed}: median rent burden {final.MedianRentBurden:F4} " +
            $"exceeds the V5 ceiling 0.40");
        Assert.True(final.Gini <= 0.70m,
            $"{report.RequestedGenoses}-{report.Seed}: Gini {final.Gini:F4} exceeds the V5 ceiling 0.70");
        Assert.True(final.WageShare <= 1.00m,
            $"{report.RequestedGenoses}-{report.Seed}: wage share of firm revenue {final.WageShare:F4} " +
            $"exceeds the closed-loop ceiling 1.00 (firms paying out more wages than revenue)");
    }

    private static BalanceReport BuildReport(
        SimulationEngine engine,
        int requestedGenoses,
        int seed,
        Money startSupply,
        Money startSource,
        Money startSink,
        int startLiving,
        BalanceFlows flows)
    {
        var economy = engine.Economy!;
        var world = engine.World!;

        var companies = 0;
        var offices = 0;
        var postings = 0;
        var employees = 0;
        var jobSeekers = 0;
        var households = 0;
        var units = 0;
        var properties = 0;

        world.Query(in new QueryDescription().WithAll<CompanyName>(), (ref CompanyName _) => companies++);
        world.Query(in new QueryDescription().WithAll<Office>(), (ref Office _) => offices++);
        world.Query(in new QueryDescription().WithAll<JobPosting>(), (ref JobPosting _) => postings++);
        world.Query(in new QueryDescription().WithAll<Employee>(), (ref Employee _) => employees++);
        world.Query(in new QueryDescription().WithAll<JobSeeker>(), (ref JobSeeker _) => jobSeekers++);
        world.Query(in new QueryDescription().WithAll<Household>(), (ref Household _) => households++);
        world.Query(in new QueryDescription().WithAll<HousingUnit>(), (ref HousingUnit _) => units++);
        world.Query(in new QueryDescription().WithAll<Property>(), (ref Property _) => properties++);

        var endSupply = economy.TotalMoneySupply();
        var decadeGrowth = startSupply.Cents > 0
            ? (endSupply.Cents - startSupply.Cents) / (decimal)startSupply.Cents
            : 0m;
        var sourceDelta = economy.Ledger.SourceTotal - startSource;
        var sinkDelta = economy.Ledger.SinkTotal - startSink;
        var supplyDelta = endSupply - startSupply;
        var conservationGap = supplyDelta - (sourceDelta - sinkDelta);

        var rows = new List<BalanceRow>();
        var count = Math.Min(
            economy.MetricHistory.Count,
            Math.Min(economy.Life.MetricHistory.Count, economy.Housing.MetricHistory.Count));

        for (var i = 0; i < count; i++)
        {
            var e = economy.MetricHistory[i];
            var h = economy.Housing.MetricHistory[i];
            var p = economy.Life.MetricHistory[i];
            rows.Add(new BalanceRow(
                e.Year,
                e.TotalMoneySupply.Cents,
                e.HouseholdCash.Cents,
                e.HouseholdSavings.Cents,
                e.CompanyCash.Cents,
                e.PensionFund.Cents,
                e.GovernmentFund.Cents,
                e.UnemploymentRate,
                e.VacancyRate,
                e.WageShare,
                e.ProfitShare,
                e.Gini,
                e.Arrears.Cents,
                e.BankruptCompanies,
                h.VacancyRate,
                h.HomelessHouseholds,
                h.MedianRentBurden,
                h.RentArrears.Cents,
                h.UtilityArrears.Cents,
                h.EvictionsThisYear,
                h.ConstructionUnits,
                p.Total,
                p.Education,
                p.Work,
                p.Retirement,
                p.Elderly,
                p.Employed,
                p.Unemployed,
                p.DeathsThisYear,
                p.ArrivalsThisYear,
                p.BirthsThisYear,
                p.ReplacementRate,
                p.PensionFundingRatio,
                p.PensionReplacementRate,
                p.LifeExpectancy));
        }

        var latestEconomy = rows.Count > 0 ? economy.MetricHistory[^1] : null;
        var previousEconomy = rows.Count > 1 ? economy.MetricHistory[^2] : null;
        var economyWarnings = EconomyHealth.Evaluate(latestEconomy, previousEconomy)
            .Count(check => check.Level is HealthLevel.Warn or HealthLevel.Fail);
        var housingWarnings = HousingHealth.Evaluate(economy.Housing.MetricHistory.Count > 0
                ? economy.Housing.MetricHistory[^1]
                : null)
            .Count(check => check.Level is HealthLevel.Warn or HealthLevel.Fail);
        // Annual firm-exit rate, already annualized by EconomyMetricsSystem from the
        // year's liquidation flow. Dividing the cumulative Bankrupt stock by the
        // company count (the old code) reported a decade-cumulative ratio.
        var companyFailureRate = economy.MetricHistory.Count > 0
            ? economy.MetricHistory[^1].CompanyFailureRate
            : 0m;

        return new BalanceReport(
            requestedGenoses,
            seed,
            startLiving,
            economy.Life.Index.TotalLiving,
            startSupply.Cents,
            endSupply.Cents,
            decadeGrowth,
            sourceDelta.Cents,
            sinkDelta.Cents,
            supplyDelta.Cents,
            conservationGap.Cents,
            companies,
            offices,
            postings,
            employees,
            jobSeekers,
            households,
            units,
            properties,
            flows,
            rows,
            economy.Housing.MetricHistory.Count > 0 ? economy.Housing.MetricHistory[^1].OvercrowdedUnits : 0,
            economyWarnings + housingWarnings,
            companyFailureRate,
            economy.Housing.Market.UtilityCollectedThisYear.Cents,
            economy.ConsumptionThisYear.Cents);
    }

    private sealed record BalanceReport(
        int RequestedGenoses,
        int Seed,
        int StartLiving,
        int EndLiving,
        long StartMoneySupplyCents,
        long EndMoneySupplyCents,
        decimal DecadeMoneySupplyGrowth,
        long SourceDeltaCents,
        long SinkDeltaCents,
        long SupplyDeltaCents,
        long ConservationGapCents,
        int Companies,
        int Offices,
        int Postings,
        int Employees,
        int JobSeekers,
        int Households,
        int Units,
        int Properties,
        BalanceFlows Flows,
        IReadOnlyList<BalanceRow> Years,
        int OvercrowdedUnits,
        int HealthWarnings,
        decimal CompanyFailureRate,
        long UtilityCostCents,
        long GroceryCostCents);

    private sealed record BalanceFlows(
        long WagesPaidCents,
        long CompanyRevenueCents,
        long ConsumptionCents,
        long TaxCents,
        long BasicIncomeCents,
        long RentCollectedCents,
        long UtilityCollectedCents,
        long UpkeepPaidCents);

    private sealed record BalanceRow(
        int Year,
        long MoneySupplyCents,
        long HouseholdCashCents,
        long HouseholdSavingsCents,
        long CompanyCashCents,
        long PensionFundCents,
        long GovernmentFundCents,
        decimal UnemploymentRate,
        decimal VacancyRate,
        decimal WageShare,
        decimal ProfitShare,
        decimal Gini,
        long ArrearsCents,
        int BankruptCompanies,
        decimal HousingVacancyRate,
        int HomelessHouseholds,
        decimal MedianRentBurden,
        long RentArrearsCents,
        long UtilityArrearsCents,
        int Evictions,
        int ConstructionUnits,
        int Population,
        int Education,
        int Work,
        int Retirement,
        int Elderly,
        int Employed,
        int Unemployed,
        int Deaths,
        int Arrivals,
        int Births,
        decimal ReplacementRate,
        decimal PensionFundingRatio,
        decimal PensionReplacementRate,
        decimal LifeExpectancy);
}

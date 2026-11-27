using Arch.Core;
using Genelife.Application.Systems;
using Genelife.Components.Company;
using Genelife.Economy;
using Genelife.Values;

namespace Genelife.UnitTests.Economy;

/// <summary>
/// The company-failure band is one quantity — an annual firm-exit rate — carried by
/// <see cref="EconomySnapshot.CompanyFailureRate"/> and read by both the health check and
/// the balance report. These tests pin both halves of that contract: the metric is a
/// yearly flow (not the monotonic Bankrupt stock) and the check is scale-free (not an
/// absolute count). See docs/research/v7-company-failure-decision.md §4/§5.
/// </summary>
public class CompanyFailureRateTests
{
    [Fact]
    public void Health_EmitsTheSameLevelForTheSameRateAt60And2kCompanies()
    {
        // 3 failures among 60 firms and 100 among 2000 are both 5% a year.
        var smallRate = 3 / (decimal)60;
        var largeRate = 100 / (decimal)2000;
        Assert.Equal(smallRate, largeRate);

        var small = FailureCheck(smallRate, bankruptCompanies: 3);
        var large = FailureCheck(largeRate, bankruptCompanies: 100);

        Assert.Equal(small.Level, large.Level);
        Assert.Equal(HealthLevel.Pass, small.Level);
    }

    [Fact]
    public void Health_IgnoresTheBankruptCompanyCount()
    {
        // The retired absolute-count branch failed on >2 bankrupt companies. A hundred
        // liquidated firms in a year with no failures *this* year is a stale stock, not
        // a rate: only CompanyFailureRate may drive the level.
        var check = FailureCheck(0m, bankruptCompanies: 100);

        Assert.Equal(HealthLevel.Pass, check.Level);
        Assert.Contains("per year", check.Detail);
    }

    [Theory]
    [InlineData(0.0, HealthLevel.Pass)]
    [InlineData(0.02, HealthLevel.Pass)]
    [InlineData(0.15, HealthLevel.Pass)]
    [InlineData(0.16, HealthLevel.Warn)]
    [InlineData(0.30, HealthLevel.Warn)]
    [InlineData(0.31, HealthLevel.Fail)]
    public void Health_CompanyFailureRate_UsesOneBandAtEveryScale(double rate, HealthLevel expected)
    {
        Assert.Equal(expected, FailureCheck((decimal)rate).Level);
    }

    [Fact]
    public void MetricsSystem_ReportsTheYearlyFlow_NotTheCumulativeStock()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);

        for (var i = 0; i < 3; i++)
            world.Create(new CompanyName($"Company{i}"), CompanyAccount.Create(Money.FromUnits(10_000m), 0));

        // One firm liquidated during the year: the entity stays in the world, so the
        // Bankrupt stock never falls back.
        var liquidated = CompanyAccount.Create(Money.Zero, 0);
        liquidated.State = CompanySolvency.Bankrupt;
        world.Create(new CompanyName("Liquidated"), liquidated);

        economy.CompaniesFailedThisYear = 1;
        var metrics = new EconomyMetricsSystem(world, economy);

        var snapshot = metrics.Update(economy.Clock.TicksPerYear);
        Assert.Equal(1, snapshot.BankruptCompanies);
        Assert.Equal(0.25m, snapshot.CompanyFailureRate);

        // Next year: no new failures. The stock is still 1, the rate must be 0 — dividing
        // the stock by the company count (the old metric) would still report 0.25.
        var nextYear = metrics.Update(economy.Clock.TicksPerYear * 2);
        Assert.Equal(1, nextYear.BankruptCompanies);
        Assert.Equal(0m, nextYear.CompanyFailureRate);
    }

    [Fact]
    public void SolvencySystem_LiquidationIsTheYearlyFlow()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var solvency = new CompanySolvencySystem(world, economy);

        var doomed = CompanyAccount.Create(Money.Zero, 0);
        doomed.State = CompanySolvency.Insolvent;
        doomed.ReservedForPayroll = Money.FromUnits(100m);
        doomed.InsolventCycles = economy.Options.LiquidationGraceCycles;
        var doomedCompany = world.Create(new CompanyName("Doomed"), doomed);
        world.Create(new CompanyName("Steady"), CompanyAccount.Create(Money.FromUnits(50_000m), 0));

        solvency.Update(1);

        Assert.Equal(1, economy.CompaniesFailedThisYear);
        Assert.Equal(CompanySolvency.Bankrupt, world.Get<CompanyAccount>(doomedCompany).State);
    }

    [Fact]
    public void FlowCounter_IsResetWithTheOtherYearlyCounters()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        economy.CompaniesFailedThisYear = 4;

        economy.ResetYearCounters();

        Assert.Equal(0, economy.CompaniesFailedThisYear);
    }

    private static EconomyHealthCheck FailureCheck(decimal failureRate, int bankruptCompanies = 0) =>
        EconomyHealth.Evaluate(Snapshot(failureRate, bankruptCompanies), previous: null)
            .Single(check => check.Name == "company-failure-rate");

    private static EconomySnapshot Snapshot(decimal failureRate, int bankruptCompanies = 0) => new(
        Tick: 0,
        Year: 1,
        HouseholdCash: Money.Zero,
        HouseholdSavings: Money.Zero,
        CompanyCash: Money.Zero,
        CompanyDebt: Money.Zero,
        PensionFund: Money.Zero,
        GovernmentFund: Money.Zero,
        BankFunds: Money.Zero,
        TotalMoneySupply: Money.Zero,
        WagesPaidThisYear: Money.Zero,
        RevenueThisYear: Money.Zero,
        TaxThisYear: Money.Zero,
        BasicIncomeThisYear: Money.Zero,
        Arrears: Money.Zero,
        UnemploymentRate: 0.05m,
        VacancyRate: 0.05m,
        WageShare: 0.55m,
        ProfitShare: 0.45m,
        Gini: 0.2m,
        HomelessHouseholds: 0,
        BankruptCompanies: bankruptCompanies,
        CompanyFailureRate: failureRate);
}
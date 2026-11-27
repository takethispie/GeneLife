using Genelife.Values;

namespace Genelife.Economy;

public sealed record EconomySnapshot(
    long Tick,
    int Year,
    Money HouseholdCash,
    Money HouseholdSavings,
    Money CompanyCash,
    Money CompanyDebt,
    Money PensionFund,
    Money GovernmentFund,
    Money BankFunds,
    Money TotalMoneySupply,
    Money WagesPaidThisYear,
    Money RevenueThisYear,
    Money TaxThisYear,
    Money BasicIncomeThisYear,
    Money Arrears,
    decimal UnemploymentRate,
    decimal VacancyRate,
    decimal WageShare,
    decimal ProfitShare,
    decimal Gini,
    int HomelessHouseholds,
    int BankruptCompanies,
    decimal CompanyFailureRate);

public enum HealthLevel
{
    Pass,
    Warn,
    Fail
}

public sealed record EconomyHealthCheck(string Name, HealthLevel Level, string Detail);

public static class EconomyHealth
{
    // External annual firm-exit band: 0.02-0.15/yr (US ~0.075).
    // The lower bound is deliberately disabled until the rebalance makes failure
    // reachable (no liquidation happens today, so a two-sided band would fail the
    // gate on a metric the mechanism cannot yet move). Flip it to 0.02m once a
    // non-zero flow exists — v7-company-failure-decision.md §5.4.
    private const decimal CompanyFailureLowerBound = 0m;
    private const decimal CompanyFailureWarnHigh = 0.15m;
    private const decimal CompanyFailureFailHigh = 0.30m;

    public static List<EconomyHealthCheck> Evaluate(EconomySnapshot? current, EconomySnapshot? previous)
    {
        var checks = new List<EconomyHealthCheck>();

        if (current is null)
        {
            checks.Add(new EconomyHealthCheck("snapshot", HealthLevel.Warn, "No economy snapshot yet."));
            return checks;
        }

        var supplyGrowth = previous is not null && previous.TotalMoneySupply.Cents > 0
            ? (current.TotalMoneySupply.Cents - previous.TotalMoneySupply.Cents) / (decimal)previous.TotalMoneySupply.Cents
            : 0m;

        checks.Add(Band("money-supply-growth", supplyGrowth, 0m, 0.15m, 0.30m, "per year"));

        checks.Add(current.UnemploymentRate switch
        {
            > 0.20m => new("unemployment", HealthLevel.Fail, $"{current.UnemploymentRate:P1}"),
            > 0.10m => new("unemployment", HealthLevel.Warn, $"{current.UnemploymentRate:P1}"),
            _ => new("unemployment", HealthLevel.Pass, $"{current.UnemploymentRate:P1}")
        });

        // WageShare is net wage cost as a share of firm revenue. It is NOT the
        // ILO labour income share (a share of value added): GeneLife models no
        // intermediate inputs and no separate capital factor, so the ratio is
        // expected to run far above 52%. The applicable bound is the closed-loop
        // one: a firm sector cannot sustain paying out more wages than it
        // receives in revenue, so 1.00 is the structural ceiling and 0.90 the
        // early warning. Below 0.30 capital and land absorb almost all firm
        // income and household income collapses.
        checks.Add(current.WageShare switch
        {
            < 0.30m or > 1.00m => new("wage-share", HealthLevel.Fail, $"{current.WageShare:P1}"),
            < 0.40m or > 0.90m => new("wage-share", HealthLevel.Warn, $"{current.WageShare:P1}"),
            _ => new("wage-share", HealthLevel.Pass, $"{current.WageShare:P1}")
        });

        var arrearsShare = current.WagesPaidThisYear.IsPositive
            ? current.Arrears.Cents / (decimal)(current.Arrears.Cents + current.WagesPaidThisYear.Cents)
            : 0m;
        checks.Add(arrearsShare switch
        {
            > 0.25m => new("arrears-share", HealthLevel.Fail, $"{arrearsShare:P1}"),
            > 0.10m => new("arrears-share", HealthLevel.Warn, $"{arrearsShare:P1}"),
            _ => new("arrears-share", HealthLevel.Pass, $"{arrearsShare:P1}")
        });

        // Same quantity, same thresholds as the acceptance band: an annual firm-exit
        // rate. A fixed count meant ">2" was >40% of companies at scale 60 but >1.6%
        // at scale 2k — the same name, ~25x stricter (v7-company-failure-decision.md §4).
        checks.Add(Band(
            "company-failure-rate",
            current.CompanyFailureRate,
            CompanyFailureLowerBound,
            CompanyFailureWarnHigh,
            CompanyFailureFailHigh,
            "per year"));

        return checks;
    }

    private static EconomyHealthCheck Band(
        string name,
        decimal value,
        decimal lower,
        decimal warnHigh,
        decimal failHigh,
        string suffix)
    {
        if (value < lower)
            return new(name, HealthLevel.Fail, $"{value:P1} {suffix}");
        if (value > failHigh)
            return new(name, HealthLevel.Fail, $"{value:P1} {suffix}");
        if (value > warnHigh)
            return new(name, HealthLevel.Warn, $"{value:P1} {suffix}");
        return new(name, HealthLevel.Pass, $"{value:P1} {suffix}");
    }
}

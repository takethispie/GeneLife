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
    int BankruptCompanies);

public enum HealthLevel
{
    Pass,
    Warn,
    Fail
}

public sealed record EconomyHealthCheck(string Name, HealthLevel Level, string Detail);

public static class EconomyHealth
{
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

        checks.Add(current.WageShare switch
        {
            < 0.30m or > 0.90m => new("wage-share", HealthLevel.Fail, $"{current.WageShare:P1}"),
            < 0.40m or > 0.80m => new("wage-share", HealthLevel.Warn, $"{current.WageShare:P1}"),
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

        checks.Add(current.BankruptCompanies switch
        {
            0 => new("bankrupt-companies", HealthLevel.Pass, "0 bankrupt"),
            <= 2 => new("bankrupt-companies", HealthLevel.Warn, $"{current.BankruptCompanies} bankrupt"),
            _ => new("bankrupt-companies", HealthLevel.Fail, $"{current.BankruptCompanies} bankrupt")
        });

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

using Genelife.Values;

namespace Genelife.Economy;

/// <summary>
/// Tunable economy policy. Defaults are deliberately small relative to the
/// existing test salaries so a company with 10,000 units of capital can survive
/// several quarters while creating real cash-flow pressure.
/// </summary>
public sealed class EconomyOptions
{
    public Money InitialCompanyCapital { get; set; } = Money.FromUnits(10_000m);
    public Money InitialCompanyReserve { get; set; } = Money.FromUnits(2_500m);
    public Money InitialBankCapital { get; set; } = Money.FromUnits(100_000m);

    public decimal IncomeTaxRate { get; set; } = 0.12m;
    public decimal EmployeePensionRate { get; set; } = 0.05m;
    public decimal EmployerPensionRate { get; set; } = 0.05m;
    public decimal CorporateTaxRate { get; set; } = 0.15m;
    public int LossCarryForwardYears { get; set; } = 3;

    public Money UniversalBasicIncome { get; set; } = Money.FromUnits(50m);
    public int BasicIncomeIntervalTicks { get; set; } = 100;

    public Money MonthlyConsumptionBudget { get; set; } = Money.FromUnits(100m);
    public decimal FoodShare { get; set; } = 0.45m;
    public decimal HygieneShare { get; set; } = 0.15m;
    public decimal LeisureShare { get; set; } = 0.20m;
    public decimal HealthShare { get; set; } = 0.10m;
    public decimal DurableShare { get; set; } = 0.10m;

    public decimal RecruitmentRate { get; set; } = 0.25m;
    public int SeveranceCycles { get; set; } = 2;
    public int RepostDelayTicks { get; set; } = 300;

    public Money DefaultOfficeOperatingCost { get; set; } = Money.FromUnits(200m);

    public Money SavingsBuffer { get; set; } = Money.FromUnits(300m);
    public Money MinimumCash { get; set; } = Money.FromUnits(100m);

    public decimal LoanAnnualRate { get; set; } = 0.08m;
    public decimal DebtToRevenueLimit { get; set; } = 1.5m;
    public Money MaximumLoan { get; set; } = Money.FromUnits(20_000m);
    public int LoanTermCycles { get; set; } = 8;
    public int InsolvencyGraceCycles { get; set; } = 3;
    public int LiquidationGraceCycles { get; set; } = 6;

    public decimal HealthcareShareOfGovernment { get; set; } = 0.25m;

    public static EconomyOptions Default { get; } = new();
}

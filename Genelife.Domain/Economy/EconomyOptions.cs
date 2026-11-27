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

    /// <summary>
    /// Capital for each company created by the population seed. Kept separate
    /// from <see cref="InitialCompanyCapital"/> because a seeded world needs
    /// enough working capital per worker for payroll to survive a decade and
    /// for UBI-only money creation to stay inside the 0-10% per decade target.
    /// </summary>
    public Money SeededCompanyCapital { get; set; } = Money.FromUnits(1_200_000m);

    /// <summary>
    /// Government endowment per seeded genos. This is the buffer that funds
    /// pensions, benefits and municipal services before tax revenue arrives.
    /// </summary>
    public Money SeededGovernmentFundPerGenos { get; set; } = Money.FromUnits(10_000m);

    /// <summary>
    /// Pre-funded pension balance per seeded genos, transferred from the
    /// government endowment so the fund starts inside the funding-ratio band.
    /// </summary>
    public Money SeededPensionFundPerGenos { get; set; } = Money.FromUnits(6_000m);

    /// <summary>
    /// Share of working-stage adults deliberately left without a seat at seed
    /// time. Creates the frictional unemployment the balance targets expect.
    /// </summary>
    public decimal SeedUnemploymentRate { get; set; } = 0.05m;

    /// <summary>
    /// Labour-market stabiliser. Job growth triggers when unemployment rises
    /// above <see cref="TargetUnemploymentRate"/> plus
    /// <see cref="JobGrowthTrigger"/>, and creates government-funded companies
    /// of <see cref="JobGrowthSeatsPerCompany"/> seats.
    /// </summary>
    public decimal TargetUnemploymentRate { get; set; } = 0.06m;
    public decimal JobGrowthTrigger { get; set; } = 0.01m;
    public Money JobGrowthCompanyCapital { get; set; } = Money.FromUnits(300_000m);
    public int JobGrowthSeatsPerCompany { get; set; } = 10;
    public int JobGrowthMaxPerYear { get; set; } = 3;

    public decimal IncomeTaxRate { get; set; } = 0.12m;
    public decimal EmployeePensionRate { get; set; } = 0.05m;
    public decimal EmployerPensionRate { get; set; } = 0.05m;
    public decimal CorporateTaxRate { get; set; } = 0.15m;
    public int LossCarryForwardYears { get; set; } = 3;

    public Money UniversalBasicIncome { get; set; } = Money.FromUnits(50m);
    public int BasicIncomeIntervalTicks { get; set; } = 100;

    public Money MonthlyConsumptionBudget { get; set; } = Money.FromUnits(200m);
    public decimal FoodShare { get; set; } = 0.45m;
    public decimal HygieneShare { get; set; } = 0.15m;
    public decimal LeisureShare { get; set; } = 0.20m;
    public decimal HealthShare { get; set; } = 0.10m;
    public decimal DurableShare { get; set; } = 0.10m;

    public decimal RecruitmentRate { get; set; } = 0.25m;
    public int SeveranceCycles { get; set; } = 2;
    public int RepostDelayTicks { get; set; } = 300;

    /// <summary>
    /// Recurring per-office overhead charged every pay cycle. Sized so a
    /// seeded company's cash is not a decade-long payroll runway: at the old
    /// $200/cycle, income covered the operating cost trivially and company
    /// cash grew to ~100x one cycle's payroll reserve, so the solvency ladder
    /// (Healthy -> Tight -> Arrears -> Insolvent -> Bankrupt) was never
    /// entered and no company could ever fail (V7 decision doc §3). At
    /// $8,000/cycle the overhead is comparable to a full pay cycle's wage
    /// bill, so a minority of companies that lose their revenue footing run
    /// down through Arrears into liquidation within the decade while the
    /// economy-wide balance bands stay green.
    /// </summary>
    public Money DefaultOfficeOperatingCost { get; set; } = Money.FromUnits(8_000m);

    public Money SavingsBuffer { get; set; } = Money.FromUnits(1_000m);
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

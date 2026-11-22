namespace Genelife.Api.Persistence;

public sealed class PersistedWorldState
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; }
    public long Tick { get; set; }
    public int RandomSeed { get; set; } = 1;
    public List<PersistedSim> Sims { get; set; } = [];
    public List<PersistedCompany> Companies { get; set; } = [];
    public List<PersistedOffice> Offices { get; set; } = [];
    public List<PersistedPosting> Postings { get; set; } = [];
    public List<PersistedApplication> Applications { get; set; } = [];
    public List<PersistedLoan> Loans { get; set; } = [];
    public List<PersistedPendingRevenue> PendingRevenue { get; set; } = [];
    public List<PersistedEconomySnapshot> MetricHistory { get; set; } = [];
    public PersistedFiscal Fiscal { get; set; } = new();
}

public sealed class PersistedSim
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public ulong NetworkId { get; set; }
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public float TimeRemaining { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsLookingForWork { get; set; }
    public string? PlayerId { get; set; }

    // v1 compatibility: float units. v2 writes cents.
    public float WalletBalance { get; set; }
    public long WalletBalanceCents { get; set; }

    public long SavingsBalanceCents { get; set; }
    public long UnmetDemandCents { get; set; }
    public long LastSpendCents { get; set; }
    public long ConsumptionLifetimeCents { get; set; }
    public long PensionEmployeeCents { get; set; }
    public long PensionEmployerCents { get; set; }
    public int PensionCycles { get; set; }

    public ulong EmployerNetworkId { get; set; }
    public ulong OfficeNetworkId { get; set; }
    public string Position { get; set; } = string.Empty;
    public long SalaryCents { get; set; }

    public ulong ClaimCompanyNetworkId { get; set; }
    public long UnpaidGrossCents { get; set; }
    public long UnpaidNetCents { get; set; }
    public long SeveranceCents { get; set; }
    public int ClaimCycles { get; set; }
    public long ClaimFirstTick { get; set; }
}

public sealed class PersistedCompany
{
    public string Name { get; set; } = string.Empty;
    public ulong NetworkId { get; set; }
    public long BalanceCents { get; set; }
    public long ReservedCents { get; set; }
    public long RevenueLastCycleCents { get; set; }
    public long WagesLastCycleCents { get; set; }
    public long CostsLastCycleCents { get; set; }
    public long InterestPaidLastCycleCents { get; set; }
    public long ArrearsCents { get; set; }
    public long TaxArrearsCents { get; set; }
    public long LossCarryForwardCents { get; set; }
    public long LossCarryForwardExpiresAtTick { get; set; }
    public string Solvency { get; set; } = "Healthy";
    public long StateChangedTick { get; set; }
    public int ArrearsCycles { get; set; }
    public int InsolventCycles { get; set; }
    public bool HiringFrozen { get; set; }
    public bool HasAccount { get; set; }
}

public sealed class PersistedOffice
{
    public ulong NetworkId { get; set; }
    public ulong CompanyNetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public long OperatingCostCents { get; set; }
    public int SeatsUsed { get; set; }
    public bool IsClosed { get; set; }
}

public sealed class PersistedPosting
{
    public ulong NetworkId { get; set; }
    public ulong CompanyNetworkId { get; set; }
    public ulong OfficeNetworkId { get; set; }
    public string Position { get; set; } = string.Empty;
    public long SalaryCents { get; set; }
    public int Requirements { get; set; }
    public int OpenSeats { get; set; }
    public int TotalSeats { get; set; }
    public long PostedAtTick { get; set; }
    public bool IsClosed { get; set; }
}

public sealed class PersistedApplication
{
    public ulong ApplicantNetworkId { get; set; }
    public ulong PostingNetworkId { get; set; }
    public ulong CompanyNetworkId { get; set; }
    public string Position { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public float MatchScore { get; set; }
    public DateTime ApplicationDate { get; set; }
}

public sealed class PersistedLoan
{
    public ulong LoanId { get; set; }
    public ulong CompanyNetworkId { get; set; }
    public long PrincipalCents { get; set; }
    public long OutstandingCents { get; set; }
    public decimal AnnualRate { get; set; }
    public int TermCycles { get; set; }
    public int CyclesRemaining { get; set; }
    public long PaymentPerCycleCents { get; set; }
    public string State { get; set; } = "Active";
    public int MissedPayments { get; set; }
    public long IssuedAtTick { get; set; }
}

public sealed class PersistedPendingRevenue
{
    public ulong CompanyNetworkId { get; set; }
    public ulong SimNetworkId { get; set; }
    public string Category { get; set; } = string.Empty;
    public long AmountCents { get; set; }
    public long Tick { get; set; }
}

public sealed class PersistedEconomySnapshot
{
    public long Tick { get; set; }
    public int Year { get; set; }
    public long HouseholdCashCents { get; set; }
    public long HouseholdSavingsCents { get; set; }
    public long CompanyCashCents { get; set; }
    public long CompanyDebtCents { get; set; }
    public long PensionFundCents { get; set; }
    public long GovernmentFundCents { get; set; }
    public long BankFundsCents { get; set; }
    public long TotalMoneySupplyCents { get; set; }
    public long WagesPaidCents { get; set; }
    public long RevenueCents { get; set; }
    public long TaxCents { get; set; }
    public long BasicIncomeCents { get; set; }
    public long ArrearsCents { get; set; }
    public decimal UnemploymentRate { get; set; }
    public decimal VacancyRate { get; set; }
    public decimal WageShare { get; set; }
    public decimal ProfitShare { get; set; }
    public decimal Gini { get; set; }
    public int HomelessHouseholds { get; set; }
    public int BankruptCompanies { get; set; }
}

public sealed class PersistedFiscal
{
    public long GovernmentBalanceCents { get; set; }
    public long IncomeTaxCollectedCents { get; set; }
    public long CorporateTaxCollectedCents { get; set; }
    public long BenefitsPaidCents { get; set; }
    public long OfficeCostsCollectedCents { get; set; }
    public long PensionBalanceCents { get; set; }
    public long PensionContributionsCents { get; set; }
    public long BankBalanceCents { get; set; }
    public long BankInterestPoolCents { get; set; }
    public long WagesPaidThisYearCents { get; set; }
    public long RevenueThisYearCents { get; set; }
    public long TaxThisYearCents { get; set; }
    public long BasicIncomeThisYearCents { get; set; }
    public long ConsumptionThisYearCents { get; set; }
}

namespace Genelife.Api.Persistence;

public sealed class PersistedWorldState
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; set; }
    public long Tick { get; set; }
    public int RandomSeed { get; set; } = 1;
    public int MigratedFromVersion { get; set; }
    public bool HousingMigrated { get; set; }
    public List<PersistedSim> Sims { get; set; } = [];
    public List<PersistedCompany> Companies { get; set; } = [];
    public List<PersistedOffice> Offices { get; set; } = [];
    public List<PersistedPosting> Postings { get; set; } = [];
    public List<PersistedApplication> Applications { get; set; } = [];
    public List<PersistedLoan> Loans { get; set; } = [];
    public List<PersistedPendingRevenue> PendingRevenue { get; set; } = [];
    public List<PersistedEconomySnapshot> MetricHistory { get; set; } = [];
    public List<PersistedProperty> Properties { get; set; } = [];
    public List<PersistedHousingUnit> HousingUnits { get; set; } = [];
    public List<PersistedHousehold> Households { get; set; } = [];
    public List<PersistedHouseholdMember> HouseholdMembers { get; set; } = [];
    public List<PersistedConstructionProject> ConstructionProjects { get; set; } = [];
    public List<PersistedHousingSnapshot> HousingMetricHistory { get; set; } = [];
    public PersistedHousingMarket HousingMarket { get; set; } = new();
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

    public ulong HouseholdNetworkId { get; set; }
    public ulong HouseholdUnitNetworkId { get; set; }
    public string Tenure { get; set; } = string.Empty;
    public string HousingTier { get; set; } = string.Empty;
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

public sealed class PersistedProperty
{
    public ulong NetworkId { get; set; }
    public ulong OwnerNetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int UnitCount { get; set; }
    public float Condition { get; set; }
    public float Neighbourhood { get; set; }
    public long MonthlyUpkeepCents { get; set; }
    public bool IsShelter { get; set; }
}

public sealed class PersistedHousingUnit
{
    public ulong NetworkId { get; set; }
    public ulong PropertyNetworkId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public string Tier { get; set; } = "Basic";
    public int Capacity { get; set; }
    public long BaseRentCents { get; set; }
    public float Condition { get; set; }
    public bool IsVacant { get; set; }
    public ulong OwnerNetworkId { get; set; }
    public long PurchasePriceCents { get; set; }
    public int OccupantCount { get; set; }
    public bool IsCondemned { get; set; }
}

public sealed class PersistedHousehold
{
    public ulong NetworkId { get; set; }
    public ulong HeadNetworkId { get; set; }
    public ulong UnitNetworkId { get; set; }
    public string Tenure { get; set; } = "Renter";
    public long MonthlyRentCents { get; set; }
    public long MoveInTick { get; set; }
    public int ArrearsCycles { get; set; }
    public long RentArrearsCents { get; set; }
    public long UtilityArrearsCents { get; set; }
    public long DebtCents { get; set; }
    public long LastRentTick { get; set; }
    public long LastUtilityTick { get; set; }
    public int NoticeStage { get; set; }
    public long NoticeStartedTick { get; set; }
    public long CureDeadlineTick { get; set; }
    public long LastMoveTick { get; set; }
    public int OverburdenMonths { get; set; }
    public bool NoUtilities { get; set; }
    public int MemberCount { get; set; }
}

public sealed class PersistedHouseholdMember
{
    public ulong HouseholdNetworkId { get; set; }
    public ulong SimNetworkId { get; set; }
    public string Role { get; set; } = "Other";
}

public sealed class PersistedConstructionProject
{
    public ulong ProjectId { get; set; }
    public ulong DeveloperNetworkId { get; set; }
    public string District { get; set; } = string.Empty;
    public string Tier { get; set; } = "Basic";
    public int Units { get; set; }
    public long CostCents { get; set; }
    public long PaidToDateCents { get; set; }
    public int MonthsRemaining { get; set; }
    public int TotalMonths { get; set; }
    public long StartedAtTick { get; set; }
}

public sealed class PersistedHousingMarket
{
    public decimal Index { get; set; } = 1m;
    public long LastUpdatedTick { get; set; }
    public int EvictionsThisYear { get; set; }
    public int MovesThisYear { get; set; }
    public int CompletionsThisYear { get; set; }
    public long RentCollectedThisMonthCents { get; set; }
    public long UtilityCollectedThisMonthCents { get; set; }
    public long UpkeepPaidThisMonthCents { get; set; }
    public long RentCollectedThisYearCents { get; set; }
    public long UtilityCollectedThisYearCents { get; set; }
    public long UpkeepPaidThisYearCents { get; set; }
    public List<PersistedDistrict> Districts { get; set; } = [];
}

public sealed class PersistedDistrict
{
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Units { get; set; }
}

public sealed class PersistedHousingSnapshot
{
    public long Tick { get; set; }
    public int Year { get; set; }
    public int Properties { get; set; }
    public int Units { get; set; }
    public int OccupiedUnits { get; set; }
    public int VacantUnits { get; set; }
    public int ShelterBeds { get; set; }
    public int ShelteredHouseholds { get; set; }
    public int HomelessHouseholds { get; set; }
    public int OvercrowdedUnits { get; set; }
    public int UnitsBelowCondition { get; set; }
    public decimal VacancyRate { get; set; }
    public decimal RentIndex { get; set; }
    public long RentRollCents { get; set; }
    public long RentCollectedCents { get; set; }
    public long RentArrearsCents { get; set; }
    public long UtilityArrearsCents { get; set; }
    public long UpkeepPaidCents { get; set; }
    public int EvictionsThisYear { get; set; }
    public int ConstructionUnits { get; set; }
    public decimal MedianRentBurden { get; set; }
    public decimal ShelterOccupancy { get; set; }
}

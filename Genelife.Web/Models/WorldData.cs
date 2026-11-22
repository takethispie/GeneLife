namespace Genelife.Web.Models;

public sealed class WorldData
{
    public long Tick { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsRunning { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public List<SimData> Sims { get; set; } = new();
    public List<CompanyData> Companies { get; set; } = new();
    public List<OfficeData> Offices { get; set; } = new();
    public List<EmployeeData> Employees { get; set; } = new();
    public List<JobPostingData> JobPostings { get; set; } = new();
    public List<JobApplicationData> JobApplications { get; set; } = new();
    public List<OpenPositionData> OpenPositions { get; set; } = new();
    public EconomyData Economy { get; set; } = new();
    public HousingData Housing { get; set; } = new();
    public LifecycleData Lifecycle { get; set; } = new();
    public List<LedgerEntryData> RecentLedger { get; set; } = new();
}

public sealed class SimData
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsPlayerControlled { get; set; }
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string CurrentAction { get; set; } = string.Empty;
    public float TimeRemaining { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public bool HasMoveTarget { get; set; }
    public int MoveTargetX { get; set; }
    public int MoveTargetY { get; set; }
    public decimal WalletBalance { get; set; }
    public decimal SavingsBalance { get; set; }
    public decimal UnmetDemand { get; set; }
    public ulong? HouseholdNetworkId { get; set; }
    public ulong? HousingUnitNetworkId { get; set; }
    public string Tenure { get; set; } = string.Empty;
    public string HousingTier { get; set; } = string.Empty;
    public bool Homeless { get; set; }
    public string Stage { get; set; } = string.Empty;
    public decimal Health { get; set; }
    public string TopSkill { get; set; } = string.Empty;
    public string Seniority { get; set; } = string.Empty;
    public bool IsDead { get; set; }
}

public sealed class CompanyData
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int OfficeCount { get; set; }
    public decimal Balance { get; set; }
    public decimal ReservedForPayroll { get; set; }
    public decimal Arrears { get; set; }
    public decimal RevenueLastCycle { get; set; }
    public decimal WagesLastCycle { get; set; }
    public string Solvency { get; set; } = string.Empty;
}

public sealed class OfficeData
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EmployeeCount { get; set; }
    public int OpenSeats { get; set; }
    public int SeatsUsed { get; set; }
    public decimal OperatingCost { get; set; }
    public bool IsClosed { get; set; }
}

public sealed class EmployeeData
{
    public ulong SimNetworkId { get; set; }
    public string Sim { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public float Salary { get; set; }
}

public sealed class JobPostingData
{
    public ulong NetworkId { get; set; }
    public string Position { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public float Salary { get; set; }
    public string Requirements { get; set; } = string.Empty;
    public int OpenPositions { get; set; }
    public int TotalSeats { get; set; }
    public long PostedAtTick { get; set; }
    public bool IsClosed { get; set; }
}

public sealed class JobApplicationData
{
    public string Applicant { get; set; } = string.Empty;
    public ulong ApplicantNetworkId { get; set; }
    public string Position { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public float MatchScore { get; set; }
    public DateTime ApplicationDate { get; set; }
}

public sealed class OpenPositionData
{
    public string Position { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public float Salary { get; set; }
}


public sealed class EconomyData
{
    public decimal HouseholdCash { get; set; }
    public decimal HouseholdSavings { get; set; }
    public decimal CompanyCash { get; set; }
    public decimal CompanyDebt { get; set; }
    public decimal PensionFund { get; set; }
    public decimal GovernmentFund { get; set; }
    public decimal BankFunds { get; set; }
    public decimal TotalMoneySupply { get; set; }
    public decimal Arrears { get; set; }
    public decimal WagesPaidThisYear { get; set; }
    public decimal RevenueThisYear { get; set; }
    public decimal TaxThisYear { get; set; }
    public decimal BasicIncomeThisYear { get; set; }
    public decimal UnemploymentRate { get; set; }
    public decimal VacancyRate { get; set; }
    public decimal WageShare { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public List<EconomyHealthCheckData> Health { get; set; } = new();
}

public sealed class EconomyHealthCheckData
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class LedgerEntryData
{
    public Guid TransactionId { get; set; }
    public long Tick { get; set; }
    public string Kind { get; set; } = string.Empty;
    public ulong? FromNetworkId { get; set; }
    public ulong? ToNetworkId { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Memo { get; set; }
}

public sealed class EconomySnapshotData
{
    public long Tick { get; set; }
    public int Year { get; set; }
    public decimal HouseholdCash { get; set; }
    public decimal HouseholdSavings { get; set; }
    public decimal CompanyCash { get; set; }
    public decimal CompanyDebt { get; set; }
    public decimal PensionFund { get; set; }
    public decimal GovernmentFund { get; set; }
    public decimal TotalMoneySupply { get; set; }
    public decimal WagesPaid { get; set; }
    public decimal Revenue { get; set; }
    public decimal Tax { get; set; }
    public decimal Arrears { get; set; }
    public decimal UnemploymentRate { get; set; }
    public decimal VacancyRate { get; set; }
    public decimal WageShare { get; set; }
    public decimal Gini { get; set; }
    public int BankruptCompanies { get; set; }
}

public sealed class FiscalData
{
    public decimal GovernmentBalance { get; set; }
    public decimal IncomeTaxCollected { get; set; }
    public decimal CorporateTaxCollected { get; set; }
    public decimal BenefitsPaid { get; set; }
    public decimal OfficeCostsCollected { get; set; }
    public decimal PensionBalance { get; set; }
    public decimal PensionContributions { get; set; }
    public decimal BankBalance { get; set; }
    public decimal BankInterestPool { get; set; }
    public decimal TotalLoanOutstanding { get; set; }
}

public sealed class HousingData
{
    public int Properties { get; set; }
    public int Units { get; set; }
    public int OccupiedUnits { get; set; }
    public int VacantUnits { get; set; }
    public int ShelterBeds { get; set; }
    public int ShelteredHouseholds { get; set; }
    public int HomelessHouseholds { get; set; }
    public int OvercrowdedUnits { get; set; }
    public int UnitsBelowCondition { get; set; }
    public int ActiveProjects { get; set; }
    public int ConstructionUnits { get; set; }
    public decimal VacancyRate { get; set; }
    public decimal RentIndex { get; set; }
    public decimal RentRoll { get; set; }
    public decimal RentCollectedThisMonth { get; set; }
    public decimal UtilityCollectedThisMonth { get; set; }
    public decimal UpkeepPaidThisMonth { get; set; }
    public decimal RentArrears { get; set; }
    public decimal UtilityArrears { get; set; }
    public decimal MedianRentBurden { get; set; }
    public decimal ShelterOccupancy { get; set; }
    public int EvictionsThisYear { get; set; }
    public int MovesThisYear { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public List<HousingHealthCheckData> Health { get; set; } = new();
}

public sealed class HousingHealthCheckData
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class HousingPropertyData
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public ulong? OwnerNetworkId { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string OwnerType { get; set; } = string.Empty;
    public int UnitCount { get; set; }
    public int OccupiedUnits { get; set; }
    public int VacantUnits { get; set; }
    public float Condition { get; set; }
    public float Neighbourhood { get; set; }
    public decimal MonthlyUpkeep { get; set; }
    public bool IsShelter { get; set; }
    public List<HousingUnitData> Units { get; set; } = new();
}

public sealed class HousingUnitData
{
    public ulong NetworkId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Occupants { get; set; }
    public decimal BaseRent { get; set; }
    public decimal MarketRent { get; set; }
    public decimal PurchasePrice { get; set; }
    public float Condition { get; set; }
    public bool IsVacant { get; set; }
    public bool IsOvercrowded { get; set; }
    public bool IsCondemned { get; set; }
    public ulong? HouseholdNetworkId { get; set; }
    public string Household { get; set; } = string.Empty;
}

public sealed class HouseholdData
{
    public ulong NetworkId { get; set; }
    public ulong HeadNetworkId { get; set; }
    public string Head { get; set; } = string.Empty;
    public ulong? UnitNetworkId { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string Tenure { get; set; } = string.Empty;
    public int Members { get; set; }
    public int Earners { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal RentArrears { get; set; }
    public decimal UtilityArrears { get; set; }
    public decimal Debt { get; set; }
    public int ArrearsCycles { get; set; }
    public int NoticeStage { get; set; }
    public long CureDeadlineTick { get; set; }
    public bool Overcrowded { get; set; }
    public bool NoUtilities { get; set; }
    public bool Homeless { get; set; }
    public List<string> MembersList { get; set; } = new();
}

public sealed class HousingRentRollData
{
    public ulong? OwnerNetworkId { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string OwnerType { get; set; } = string.Empty;
    public int Units { get; set; }
    public int OccupiedUnits { get; set; }
    public decimal RentRoll { get; set; }
    public decimal RentArrears { get; set; }
    public decimal UtilityArrears { get; set; }
}

public sealed class HousingSnapshotData
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
    public decimal RentRoll { get; set; }
    public decimal RentCollected { get; set; }
    public decimal RentArrears { get; set; }
    public decimal UtilityArrears { get; set; }
    public decimal UpkeepPaid { get; set; }
    public int EvictionsThisYear { get; set; }
    public int ConstructionUnits { get; set; }
    public decimal MedianRentBurden { get; set; }
    public decimal ShelterOccupancy { get; set; }
}

public sealed class HousingConstructionData
{
    public ulong NetworkId { get; set; }
    public ulong? DeveloperNetworkId { get; set; }
    public string Developer { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int Units { get; set; }
    public int MonthsRemaining { get; set; }
    public decimal Cost { get; set; }
    public decimal PaidToDate { get; set; }
}

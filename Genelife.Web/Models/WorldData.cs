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
    public decimal WalletBalance { get; set; }
    public decimal SavingsBalance { get; set; }
    public decimal UnmetDemand { get; set; }
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

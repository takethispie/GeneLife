namespace Genelife.Api.DTOs;

public sealed class EconomyStatus
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
    public List<EconomyHealthCheckStatus> Health { get; set; } = new();
}

public sealed class EconomyHealthCheckStatus
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class LedgerEntryStatus
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

public sealed class EconomySnapshotStatus
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

public sealed class FiscalStatus
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

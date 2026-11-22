namespace Genelife.Messages.Events;

public record WagesPaid(
    ulong CompanyId,
    ulong SimId,
    long GrossCents,
    long NetCents,
    long TaxCents,
    long PensionCents,
    long Tick);

public record WagesDeferred(
    ulong CompanyId,
    ulong SimId,
    long UnpaidGrossCents,
    long UnpaidNetCents,
    long Tick);

public record PayrollShortfall(
    ulong CompanyId,
    long UnpaidCents,
    long BalanceCents,
    long Tick);

public record PayrollCycleCompleted(
    ulong CompanyId,
    long GrossCents,
    long CashNeededCents,
    long BalanceCents,
    long Tick);

public record WageArrearsSettled(
    ulong CompanyId,
    ulong SimId,
    long AmountCents,
    long Tick);

public record EmployeeResigned(ulong CompanyId, ulong SimId);

public record BasicIncomeBatchPaid(int Recipients, long TotalCents, long Tick);

public record ConsumerSpendingBatchCompleted(int Households, long SpentCents, long UnmetCents, long Tick);

public record PurchaseBooked(
    ulong SimId,
    ulong CompanyId,
    string Category,
    long AmountCents,
    long Tick);

public record UnmetDemandRecorded(int Households, long TotalCents, long Tick);

public record CompanyRevenueBooked(
    ulong CompanyId,
    long AmountCents,
    long Tick,
    string CategoryBreakdown);

public record OfficeCostCharged(
    ulong CompanyId,
    long TotalCents,
    long PaidCents,
    long Tick);

public record CorporateTaxDeferred(ulong CompanyId, long TaxCents, long Tick);

public record CorporateTaxAssessed(
    ulong CompanyId,
    long ProfitCents,
    long TaxPaidCents,
    long Tick);

public record RecruitmentFeeCharged(
    ulong CompanyId,
    ulong SimId,
    long FeeCents,
    long Tick);

public record CompanyRestructured(ulong CompanyId, string Actions, long Tick);

public record CompanyLiquidated(
    ulong CompanyId,
    long ClaimsPaidCents,
    long WrittenOffCents,
    long TaxArrearsCents,
    long Tick);

public record SavingsDeposited(ulong SimId, long AmountCents, long Tick);

public record SavingsWithdrawn(ulong SimId, long AmountCents, long Tick);

public record SavingsInterestPaid(ulong SimId, long AmountCents, long Tick);

public record LoanIssued(
    ulong LoanId,
    ulong CompanyId,
    long AmountCents,
    decimal AnnualRate,
    long PaymentPerCycleCents,
    long Tick);

public record LoanPaymentMade(
    ulong LoanId,
    ulong CompanyId,
    long PaidCents,
    long InterestCents,
    long PrincipalCents,
    long OutstandingCents,
    long Tick);

public record LoanPaymentMissed(
    ulong LoanId,
    ulong CompanyId,
    int MissedPayments,
    long Tick);

public record LoanDefaulted(ulong LoanId, ulong CompanyId, long Tick);

public record LedgerEntryRecorded(
    Guid TransactionId,
    long Tick,
    string Kind,
    ulong? FromNetworkId,
    ulong? ToNetworkId,
    long AmountCents,
    string Reference,
    string? Memo);

public record OfficeCreated(
    ulong OfficeId,
    ulong CompanyId,
    string Name,
    int Capacity,
    long OperatingCostCents);

public record OfficeUpdated(ulong OfficeId, string Name, int Capacity, long OperatingCostCents);

public record OfficeClosed(ulong OfficeId);

public record JobPostingOpened(
    ulong PostingId,
    ulong CompanyId,
    ulong OfficeId,
    string Position,
    long SalaryCents,
    int Seats);

public record JobPostingClosed(ulong PostingId, ulong CompanyId);

public record JobSeatReserved(ulong PostingId, int Seats);

public record JobSeatReleased(ulong PostingId, int Seats);

public record CapitalInjected(ulong CompanyId, long AmountCents, long Tick);

public record EconomySnapshotTaken(
    long Tick,
    int Year,
    long HouseholdCashCents,
    long HouseholdSavingsCents,
    long CompanyCashCents,
    long CompanyDebtCents,
    long PensionFundCents,
    long GovernmentFundCents,
    long TotalMoneySupplyCents,
    long WagesPaidCents,
    long RevenueCents,
    long TaxCents,
    long ArrearsCents,
    decimal UnemploymentRate,
    decimal VacancyRate,
    decimal WageShare,
    decimal Gini,
    int BankruptCompanies);

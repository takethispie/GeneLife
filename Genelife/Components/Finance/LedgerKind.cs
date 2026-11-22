namespace Genelife.Components.Finance;

/// <summary>
/// Every economy mutation has a ledger kind. Source kinds have no payer; sink
/// kinds have no payee. All other kinds move money from one account to another.
/// </summary>
public enum LedgerKind
{
    CapitalInjected,
    BasicIncome,
    WagesPaid,
    WagesDeferred,
    WageArrearsSettled,
    ConsumerSpending,
    CompanyRevenue,
    OfficeCost,
    RentPaid,
    RentArrears,
    RentDeposit,
    UtilityPaid,
    ShelterFee,
    MaintenancePaid,
    MovingFee,
    IncomeTaxPaid,
    CorporateTaxPaid,
    PensionContribution,
    PensionPaid,
    RecruitmentCost,
    SeverancePaid,
    LoanIssued,
    LoanInterest,
    LoanPrincipal,
    PropertyPurchase,
    PropertySale,
    ConstructionCost,
    Inheritance,
    SavingsDeposit,
    SavingsWithdrawal,
    SavingsInterest,
    WriteOff,
    Grant
}

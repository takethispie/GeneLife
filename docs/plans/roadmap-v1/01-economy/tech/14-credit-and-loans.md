# E14 — Credit and loans

**Phase:** 8 — Resilience
**Depends:** E13
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Give solvent-but-tight companies a way to survive temporary shortfalls without minting money, and make debt a real obligation with interest and default.

## Context

Without credit, the first bad quarter causes immediate arrears and a death spiral. With free credit, bankruptcy never happens. A priced credit line provides the middle ground.

## Design

```csharp
public enum LoanState { Active, Delinquent, Defaulted, Repaid }

public record struct Loan
{
    public ulong LoanId;
    public Money Principal;
    public Money Outstanding;
    public float AnnualRate;
    public int TermCycles;
    public int CyclesRemaining;
    public Money PaymentPerCycle;
    public LoanState State;
    public int MissedPayments;
    public long IssuedAtTick;
}
```

`LoanBook` singleton indexes loans by company. `CreditSystem` order 538, cadence 300.

### Eligibility

- Company state `Healthy` or `Tight`.
- No active delinquency.
- Outstanding debt ≤ `DebtToRevenueLimit × annualised revenue` (default 1.5).
- Requested amount ≤ configured maximum.

### Pricing

- Base rate plus a spread based on leverage and arrears history.
- Interest accrues each cycle on outstanding balance; payment is interest first, then principal.
- Interest is credited to `Bank.InterestPool` (E22), which funds savings interest. Loan interest is never minted.
- Early repayment is allowed with no penalty.

### Repayment

- Debit the company account at order 538, before solvency assessment.
- If cash is short, the payment is missed: state becomes `Delinquent`, `MissedPayments++`, interest continues to accrue.
- Three consecutive missed payments → `Defaulted`; the loan is handed to liquidation (E15).

Household credit is out of scope for V1. Housing arrears are handled by the housing feature without interest, to avoid a second debt engine.

## Tasks

- [ ] Add `Loan`, `LoanState`, `LoanBook`.
- [ ] Implement `CreditSystem` with collect/decide/apply.
- [ ] Add `LoanIssued`, `LoanPaymentMade`, `LoanPaymentMissed`, `LoanDefaulted` events.
- [ ] Add ledger kinds `LoanIssued`, `LoanInterest`, `LoanPrincipal`.
- [ ] Add `POST /api/companies/{id}/loans` and `POST /api/loans/{id}/repay`.
- [ ] Expose debt, leverage, rate and next payment in the company read model.
- [ ] Add a debt panel to the economy admin page.
- [ ] Enforce a global credit cap so total lending cannot outrun revenue.

## Files

- new `Genelife/Components/Finance/Loan.cs`, `LoanBook.cs`
- new `Genelife/Systems/CreditSystem.cs`
- new `Genelife/CreditPolicy.cs`
- `Genelife.Messages/Events/Loan*.cs`
- `Genelife.API/Endpoints/Wolverine/LoanEndpoints.cs`
- `Genelife.API/DTOs/CompanyStatus.cs`

## Acceptance

- An eligible company receives a loan and its account is credited exactly once.
- Interest and principal are ledgered separately.
- A missed payment marks delinquency without negative cash.
- Default hands the loan to liquidation.
- Debt cannot exceed the configured leverage limit at issuance.
- Admin can inspect and repay loans.

## Testing

- Amortisation schedule test.
- Eligibility matrix test.
- Delinquency and default test.
- Double-issuance idempotency test.
- Ledger reconciliation test.

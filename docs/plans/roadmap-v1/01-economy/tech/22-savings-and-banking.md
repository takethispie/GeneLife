# E22 — Savings and banking

**Phase:** 3 — Money
**Depends:** E01, E05, E06
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Give households a savings buffer separate from spendable cash, so unemployment, retirement and rent shocks have something to draw on without breaking money conservation.

## Context

`Wallet` is the only household money store. Housing rent and consumer spending already need a second tier ("cash, then savings"), and wealth metrics must include long-term savings. The order table reserves `BankingSystem` at 530 for this purpose.

## Design

### Accounts

```csharp
public record struct SavingsAccount
{
    public Money Balance;
    public Money LifetimeDeposited;
    public Money LifetimeWithdrawn;
    public Money InterestEarned;
    public long LastInterestTick;
}

public record struct Bank
{
    public Money InterestPool;   // funded by loan interest (E14)
    public float SavingsRate;    // annual, default 0
    public float LendingRate;    // reference rate for E14
}
```

`Bank` is a singleton entity created with the world; `SavingsAccount` is attached to adults.

### BankingSystem

Order 530, cadence 300 (quarterly), after payroll and rent:

- If cash > `cashBuffer` for the sim's stage, move the excess to savings.
- If cash < `minimumCash`, withdraw the difference from savings.
- Buffers by stage: worker 500, retiree 300, elder 150, student 50.
- Savings never goes negative; a withdrawal stops at zero.
- Interest, if enabled, is paid quarterly from `Bank.InterestPool` proportionally to balances; if the pool is empty, no interest is paid. Savings interest never mints money.
- Emit `SavingsDeposited`, `SavingsWithdrawn`, `SavingsInterestPaid`.
- Ledger kinds: `SavingsDeposit`, `SavingsWithdrawal`, `SavingsInterest`.

### Household access

`HouseholdFinanceService` exposes household savings as the sum of adult members' savings. Rent, utilities and consumer spending use the waterfall:

1. head cash;
2. other adult cash;
3. household savings;
4. arrears or unmet demand.

A child's savings account, if any, is not tapped by the household.

### Statements

The admin can inspect per-sim and aggregate savings, deposits, withdrawals and interest. Wealth metrics (`EconomySnapshot`, `PopulationSnapshot`) include savings in total household wealth.

## Tasks

- [ ] Add `SavingsAccount` and `Bank`.
- [ ] Implement `BankingSystem` at order 530.
- [ ] Add stage-based cash buffers to policy.
- [ ] Extend `HouseholdFinanceService` to withdraw from savings after cash.
- [ ] Add ledger kinds and events.
- [ ] Route loan interest into `Bank.InterestPool` (E14) and savings interest out of it.
- [ ] Expose savings in `SimStatus`, `EconomyStatus` and admin pages.
- [ ] Add a debug invariant: no negative savings; interest paid ≤ pool balance.

## Files

- new `Genelife/Components/Finance/SavingsAccount.cs`, `Bank.cs`
- new `Genelife/Systems/BankingSystem.cs`
- `Genelife.API/Services/HouseholdFinanceService.cs`
- `Genelife.API/Services/CompanyAccountService.cs` (loan interest routing)
- `Genelife.Messages/Events/Savings*.cs`
- `Genelife.API/DTOs/SimStatus.cs`, `EconomyStatus.cs`

## Acceptance

- Excess cash moves to savings and is available for rent and spending.
- Withdrawals never exceed the balance.
- Interest is paid only from the bank pool and never creates money.
- Household savings are included in affordability calculations.
- Wealth metrics include savings exactly once.
- Admin can inspect statements and totals.

## Testing

- Deposit/withdraw rule test by stage.
- Waterfall test: cash → savings → arrears.
- Interest pool funded/empty test.
- Conservation test with savings and loan interest.
- Metric inclusion test.

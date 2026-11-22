# L13 — Pension payout

**Phase:** 5 — Retirement income
**Depends:** L11, L12, E13
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Pay retirees monthly from the pension fund, handle fund and government shortfalls transparently, and stop payments exactly at death.

## Context

Retirement creates `RetirementIncome`, but no system pays it. The fund is a finite pool; payouts must reconcile with it and degrade visibly when it is exhausted.

## Design

`PensionSystem` order 505, cadence 100 (monthly). It runs immediately after payroll (500) and before rent (510), so retirement income is available before the month's rent is due.

Per retiree with `RetirementIncome`:

1. Determine due amount: base monthly pension × indexation factor.
2. Debit `PensionFund`:
   - if sufficient, pay full;
   - else draw the remainder from `GovernmentAccount` as a top-up;
   - if still short, pay pro rata across all retirees this month and set `Reduced = true`.
3. Credit the retiree wallet; ledger `PensionPaid`.
4. Emit `PensionPaid` or `PensionReduced`.

Rules:

- Payments happen once per month per retiree.
- Elders continue to receive pension unless the policy explicitly stops it at 80. V1 keeps it: elderly income is pension plus savings.
- Death before the payment tick cancels the payment; death after payment does not claw it back.
- Indexation applies at the year boundary only and only when the fund ratio ≥ 1.0.
- A retiree with no pension account receives the means-tested minimum from the government, if eligible.

Fund accounting:

- `PayoutsThisCycle` and `Shortfall` are updated;
- the fund balance never goes negative;
- government top-ups are ledgered and visible in the fiscal panel.

## Tasks

- [ ] Add `PensionSystem` with monthly payout.
- [ ] Implement fund → government → pro-rata waterfall.
- [ ] Add indexation at the year boundary.
- [ ] Emit `PensionPaid`, `PensionReduced`, `PensionFundTopUp`.
- [ ] Expose payouts, shortfall and funded ratio in the admin.
- [ ] Stop payments on death and on stage removal.
- [ ] Add admin action to top up the fund.
- [ ] Ensure no double payment in a month.

## Files

- new `Genelife/Systems/PensionSystem.cs`
- `Genelife/Components/Finance/PensionFund.cs`, `GovernmentAccount.cs`
- `Genelife/Components/Employment/RetirementIncome.cs`
- `Genelife.Messages/Events/Pension*.cs`
- `Genelife.API/Endpoints/Wolverine/EconomyEndpoints.cs`

## Acceptance

- Every eligible retiree is paid once per month.
- Full funding pays the full amount and reduces the fund exactly.
- A shortfall uses the government top-up before reducing payouts.
- Pro-rata reduction is uniform and visible.
- The fund never goes negative.
- A death before the tick prevents payment.
- Admin can see and top up the fund.

## Testing

- Full funding test.
- Government top-up test.
- Pro-rata shortfall test.
- Double-payment prevention test.
- Death cancellation test.
- Indexation gating test.

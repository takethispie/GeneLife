# Why a closed money loop

The current simulation pays wages into `Wallet` without taking the money from anywhere. That is fine for a needs demo, but it makes every later system meaningless: prices have no constraint, companies cannot fail, and housing costs do not force trade-offs.

V1 treats money as a finite quantity owned by somebody at all times.

## Principles

1. **Conservation.** Wages debit a company account. Spending credits a company account. Rent credits a landlord. Tax credits the government. Only explicit sources create money: initial company capital and initial household cash.
2. **Traceability.** Every movement is a ledger entry with a kind, source, destination, amount and tick. No silent balance edits in gameplay systems.
3. **Constraint.** A payment that cannot be funded changes state instead of succeeding: partial wages become arrears, unpaid rent becomes debt, an unfunded company becomes insolvent.
4. **Observability.** The admin can answer "where did this money go?" for any company or household without reading code.

## What the player sees

NPCs do not see a ledger. They see consequences:

- a worker whose wages were cut starts looking for a better job;
- a company that keeps losing money stops posting vacancies;
- a tenant who cannot pay rent moves to a cheaper unit or a shelter;
- a pension fund that runs dry reduces payouts.

## What the admin sees

- money supply over time, separated into household cash, household savings, company balances, pension fund and government;
- wage share, profit, tax and arrears;
- a filterable ledger per entity;
- alerts when money is created or destroyed outside the approved sources.

## Failure modes to avoid

- **Infinite wage fountain.** Payroll must debit companies, not mint money.
- **Invisible subsidy.** If a system needs money (benefits, pensions, construction), it must have a funding source and show a shortfall when empty.
- **Float drift.** Cents and a ledger make reconciliation exact; gameplay code should never compare approximate balances.
- **Overflow.** Large populations and long runs require `long` cents, not `int`.

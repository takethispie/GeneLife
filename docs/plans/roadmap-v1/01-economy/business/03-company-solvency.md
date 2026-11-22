# Company solvency and failure

A company is solvent when it can meet its obligations as they fall due. V1 makes that test explicit every pay cycle instead of assuming wages always clear.

## The obligation stack

When a pay cycle closes, a company owes, in priority order:

1. **Wages** — already worked hours.
2. **Payroll taxes and pension contributions.**
3. **Rent and utilities** for its offices.
4. **Loan interest and scheduled principal.**
5. **Recruitment and maintenance costs.**

Anything left is profit, which can be retained, distributed or invested in new offices and postings.

## Distress ladder

| State | Trigger | Behaviour |
|---|---|---|
| Healthy | cash covers obligations and reserve | posts jobs, pays full wages |
| Tight | cash covers wages but not reserve | freezes new hires, delays expansion |
| Arrears | cash cannot cover wages | pays pro rata, records arrears, attempts recovery |
| Insolvent | arrears persist for N cycles | no hiring, no expansion, credit line frozen |
| Bankrupt | no recovery path | offices closed, employees separated, assets sold, entity flagged |

The exact cycle thresholds are configuration, not hard-coded constants, so the admin can tune severity.

## Recovery options

- **Credit line.** A solvent-but-tight company can borrow against future revenue. Interest accrues and repayment is scheduled.
- **Restructuring.** Close an office, reduce seats, cut salaries, make redundancies. This is preferable to bankruptcy and keeps some jobs.
- **Capital injection.** An admin action for scenario testing or a future investor system.

## Why this matters for the population

Company failure is the main source of involuntary unemployment. Without it, every NPC who wants a job gets one and the labour market has no tension. With it:

- unemployment becomes a real state with income loss and housing risk;
- wages become a cost that shapes prices and margins;
- successful companies absorb labour and grow offices;
- the admin has meaningful economic levers.

## Safeguards

- Bankruptcy cannot destroy the last employer instantly; it separates employees over a short wind-down so the job market can react.
- Arrears are never silently forgiven. If they are written off, that is a ledger event.
- A company cannot pay dividends or distribute profit while it has arrears.
- Credit is never free; unpaid interest compounds and feeds insolvency.

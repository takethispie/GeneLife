# H05 — Rent and tenure cycle

**Phase:** 3 — Tenancy
**Depends:** H03, E05, E06, E07
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Charge rent monthly, route it to the landlord, and turn non-payment into a visible arrears state without breaking the money loop.

## Context

Households have pooled finances and a unit. Landlords are companies or owner households with accounts. The monthly boundary comes from `SimulationClock` (100 ticks).

## Design

`RentSystem` order 510, cadence 100, runs after payroll (500), so payday cash exists before rent is due.

Per household with `Tenure.Renter`:

1. Skip if `LastRentTick == current month`.
2. Compute `rentDue = MonthlyRent + UtilityBill` (utilities from H06).
3. Debit via `HouseholdFinanceService` in order: head wallet, other adults, savings.
4. Credit landlord:
   - company landlord → `CompanyAccountService.Credit`, ledger `RentPaid`;
   - household landlord → owner wallet, ledger `RentPaid`;
   - municipality → government account.
5. If partial:
   - pay what is available;
   - add remainder to `RentArrears`;
   - increment `ArrearsCycles` only when the month is fully unpaid;
   - emit `RentMissed` with the shortfall.
6. If full: clear `ArrearsCycles`, emit `RentPaid`.

Grace policy:

- first missed month: warning only;
- second: late fee (configurable, added to arrears);
- third: eligible for eviction (H08).

No rent is charged to dependents or shelter residents beyond a nominal shelter fee. Owners pay maintenance instead (H06), not rent.

Rent index changes apply at the year boundary, not monthly, and only to new tenancies or after a notice period.

## Tasks

- [ ] Add `RentSystem` with collect/decide/apply.
- [ ] Add `LastRentTick` to `Household`.
- [ ] Implement the finance waterfall and landlord routing.
- [ ] Add late fees and arrears ageing.
- [ ] Emit `RentPaid`, `RentMissed`, `LateFeeApplied`, `RentArrearsCleared`.
- [ ] Add ledger kinds `RentPaid`, `RentArrears`.
- [ ] Expose rent roll, collected, arrears and collection rate in the read model.
- [ ] Ensure no household is charged twice for the same month.

## Files

- new `Genelife/Systems/RentSystem.cs`
- `Genelife/Components/Housing/Household.cs`
- `Genelife.API/Services/HouseholdFinanceService.cs`
- `Genelife.API/Services/CompanyAccountService.cs`
- `Genelife.Messages/Events/Rent*.cs`
- `Genelife.API/DTOs/HousingStatus.cs`

## Contracts

`RentPaid(ulong HouseholdId, ulong UnitId, ulong LandlordId, long AmountCents, long Tick)`
`RentMissed(ulong HouseholdId, ulong UnitId, long DueCents, long PaidCents, int ArrearsCycles, long Tick)`

## Acceptance

- Rent is charged once per household per month.
- Full payment credits the correct landlord and leaves zero arrears.
- Partial payment records the exact shortfall and never overdraws the household.
- Arrears cycles and late fees follow the policy.
- Landlord company balance increases by rent collected.
- Ledger conservation holds across households and landlords.

## Testing

- On-time, late and partial payment tests.
- Landlord routing matrix (company, household, municipality).
- Double-charge prevention test.
- Late fee and arrears ageing test.
- Ledger reconciliation test.

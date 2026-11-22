# L16 — Estate settlement and inheritance

**Phase:** 6 — Elderly
**Depends:** L15, E06, H08
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Settle a deceased sim's debts and distribute the remainder, so money keeps circulating and housing is released or transferred correctly.

## Context

Death currently would destroy wealth with the entity. Households, rent arrears, wage claims and property ownership mean an estate is rarely just a wallet balance.

## Design

`EstateSettlementSystem` order 580, cadence 100, processes `EstateSettlementRequested` events.

### Estate composition

Assets:

- wallet cash;
- savings account;
- owned housing (sale value or transfer);
- business ownership (future);
- outstanding wage claims owed to the deceased.

Liabilities:

- rent and utility arrears of the deceased's share;
- unpaid taxes;
- loan guarantees (future);
- funeral costs (configurable flat fee).

### Order

1. Pay funeral cost.
2. Pay arrears and debts in priority order; unpaid debts are written off with a ledger entry.
3. Transfer housing:
   - if the household continues, the tenancy stays with it;
   - if the deceased owned the home and there is an heir, transfer ownership;
   - if no heir, sell the unit to the market and add proceeds to the estate.
4. Distribute remaining cash and savings:
   - household adults first, split equally;
   - then living children of the household (as dependents, held by the head);
   - then a means-tested pool of low-wealth adults;
   - if none, escheat to the government.
5. Emit `EstateSettled` with the full breakdown.

Inheritance tax is configurable (default 0%) and goes to the government. The estate never creates money: distributions equal assets minus liabilities and tax.

### Edge cases

- Negative estate: no distribution; debts beyond assets are written off.
- No living heir: escheat, visible in the fiscal panel.
- Death of the last household member: release the unit before distribution.
- Multiple deaths in one cycle: process in `NetworkId` order for determinism.

## Tasks

- [ ] Add `EstateSettlementRequested` and the queue.
- [ ] Implement asset and liability collection.
- [ ] Implement debt priority and write-off.
- [ ] Implement heir selection and distribution.
- [ ] Handle owner-occupied and rented housing.
- [ ] Add inheritance tax and funeral cost policy.
- [ ] Emit `EstateSettled`, `InheritancePaid`, `EscheatRecorded`.
- [ ] Add an estate report to the admin.
- [ ] Add invariant checks: distribution equals assets − liabilities − tax.

## Files

- new `Genelife/Systems/EstateSettlementSystem.cs`
- new `Genelife.API/Services/EstatePlanner.cs`
- `Genelife/Components/Gen/Dead.cs`
- `Genelife.API/Services/HouseholdFinanceService.cs`
- `Genelife.Messages/Events/Estate*.cs`
- `Genelife.API/DTOs/LifeStageStatus.cs`

## Acceptance

- Every death produces exactly one settlement.
- Debts are paid before distribution and never exceed assets.
- The household keeps its home when at least one member survives.
- An owner-occupied home transfers or sells correctly.
- No heir produces escheat to the government.
- Money is conserved: estate in = debts + tax + distributions.
- Admin can inspect the settlement report.

## Testing

- Simple cash estate test.
- Debt priority test.
- Negative estate test.
- Housing transfer/sale test.
- No-heir escheat test.
- Multi-death determinism test.

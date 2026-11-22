# H09 — Moving and ownership

**Phase:** 6 — Mobility
**Depends:** H05, H08
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Let households respond to rent burden and income changes by moving, and let wealthy households own their home as a store of wealth.

## Context

Without moving, a household in an unaffordable unit has only arrears and eviction. Without ownership, wealth has no long-term store and estates contain only cash.

## Design

### Voluntary moves

`HousingMoveSystem` order 512, cadence 100, evaluated monthly:

Triggers:

- rent burden > 40% of household income for two consecutive months;
- overcrowded;
- condition below 0.3;
- income increased and a better unit is affordable (small probability);
- job change makes a different district preferable (future).

Constraints:

- must pay deposit (one month rent) plus moving fee;
- target unit must be vacant and fit capacity;
- does not move during an active eviction notice unless it clears arrears;
- at most one move per household per 3 months.

Search: cheapest qualifying unit within one tier above current, preferring higher neighborhood score. Deterministic seeded choice among top three.

### Ownership

Eligibility:

- household wealth ≥ `PurchaseMultiple × annual rent` (default 20×);
- no arrears;
- owner must be an adult head.

Purchase:

- pay `unit price` to the landlord (company or household);
- transfer unit ownership to the owner household;
- tenure becomes `Owner`; rent stops; maintenance and property tax start;
- emit `HomePurchased`, ledger `PropertyPurchase`.

Sale:

- owner can list a unit; sale price = market value × (1 ± condition adjustment);
- proceeds credit the owner; if sold to another household, tenure transfers;
- if the unit is unsold for 12 months, it can be rented out (landlord mode) in a later step.

V1 does not model mortgages. Cash purchase only. Mortgage is a future extension of the credit feature.

## Tasks

- [ ] Add move triggers and cooldown to `Household`.
- [ ] Implement `HousingMoveSystem` with deposit and fee.
- [ ] Add `MoveIn`/`MoveOut` registry operations that preserve invariants.
- [ ] Implement purchase and sale in `HouseholdRepository`.
- [ ] Add `HomePurchased`, `HomeSold`, `HouseholdMoved` events.
- [ ] Add ownership fields to property/unit documents.
- [ ] Add admin actions: move household, set rent, sell unit.
- [ ] Add an invariant that an owner household does not also pay rent.

## Files

- new `Genelife/Systems/HousingMoveSystem.cs`
- `Genelife.API/Repositories/HouseholdRepository.cs`
- `Genelife/Components/Housing/Property.cs`, `HousingUnit.cs`, `Household.cs`
- `Genelife.Messages/Events/Home*.cs`, `HouseholdMoved.cs`
- `Genelife.API/Endpoints/Wolverine/HousingEndpoints.cs`

## Acceptance

- A household over the rent-burden threshold moves when an affordable unit exists.
- Moving charges deposit and fee exactly once and updates registry/residence.
- A household that cannot afford the deposit does not move.
- Purchase requires the wealth threshold and clears arrears.
- Owners pay maintenance, not rent.
- Sale transfers ownership and money correctly.
- Cooldown prevents thrashing.

## Testing

- Trigger matrix.
- Deposit affordability test.
- Move registry consistency test.
- Purchase eligibility test.
- Sale accounting test.
- Cooldown test.

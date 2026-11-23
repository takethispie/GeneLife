# P03 — Player housing and household membership

**Phase:** 3 — Housing
**Depends:** P01, V1 `HousingRegistry`, `HousingAllocator`, `RentSystem`, `HousingMoveSystem`, V4 household merge
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Let a player find and keep a home through the same capacity, tenure, rent, arrears and eviction rules as an NPC household.

## Context

`HousingRegistry` is the only membership path. `HousingUnit.Capacity` and `OccupantCount` are authoritative for crowding. `RentSystem` debits the household, tracks arrears and can evict. V4 partnership merge and household splitting already exist.

Player sims currently spawn with a household and can sleep at a property, but there is no lease, move or rent action. A player must not get a free unit or bypass capacity because they are player-controlled.

## Design

### Commands

- `RequestHousing` with `PropertyNetworkId` or `HousingUnitId`.
- `AcceptLease` with a lease id.
- `MoveOut` with an optional destination unit.
- `PayArrears` with an amount.

### Lease flow

1. The player requests a unit at the property entrance.
2. The allocator checks availability, capacity, tenure and arrears.
3. A deterministic offer is created with a rent and an expiry.
4. The player accepts the lease.
5. `HousingRegistry.AddMember`/`MoveIn` applies the move in the apply phase.
6. Rent begins on the next rent cycle.

A player may join an existing household as a partner, relative or other adult through the V4 relationship path. A player cannot join a household that has no capacity unless the existing overcrowding policy allows it.

### Player housing state

```csharp
public record struct PlayerHousing
{
    public Entity Household;
    public Entity Unit;
    public long LeaseStartTick;
    public Money Deposit;
    public bool LeaseActive;
}
```

The household, not the player sim, owns the rent obligation. The player's wallet contributes through the household finance waterfall.

### Arrears and eviction

- Rent is charged by `RentSystem` to the household.
- The player can pay arrears from their wallet or savings.
- Unpaid arrears follow the existing eviction path; a player is not exempt.
- Eviction moves the household to a shelter or a cheaper unit under the existing policy.
- A player with no home can still work and shop, but needs and health are affected.

### Moving

- `MoveOut` requires an available destination or a shelter fallback.
- Moving updates `HousingRegistry`, path caches and the player's spawn point.
- A move does not duplicate household membership or leave an orphan occupant count.

## Tasks

- [ ] Add `PlayerHousing` and lease state.
- [ ] Add `RequestHousing`, `AcceptLease`, `MoveOut` and `PayArrears` handlers.
- [ ] Reuse `HousingAllocator` for eligibility and offers.
- [ ] Apply moves through `HousingRegistry` only.
- [ ] Integrate with the V4 partnership household merge.
- [ ] Add player housing read fields and admin actions.
- [ ] Add tests for capacity, rent, arrears, eviction and move.
- [ ] Assert no orphan occupant counts after a move.

## Files

- new `Genelife/Components/Player/PlayerHousing.cs`
- new `Genelife/Player/PlayerHousingService.cs`
- `Genelife/Housing/HousingAllocator.cs`
- `Genelife/Components/Housing/HousingRegistry.cs`
- `Genelife/Systems/RentSystem.cs`
- `Genelife/Systems/HousingMoveSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Player/PlayerHousingTests.cs`

## Acceptance

- A player can request only an available unit within reach.
- Capacity, tenure and arrears rules apply to the player exactly as to an NPC.
- Accepting a lease moves the player through `HousingRegistry` and starts rent.
- A player who cannot pay follows the existing arrears and eviction path.
- Moving out does not leave an orphan occupant or a stale path cache.
- A player partnership can merge households only when capacity allows.
- The admin can inspect the lease, household and arrears.

## Testing

- Request and lease an available unit; assert membership and rent.
- Request a full unit and assert rejection.
- Stop paying rent and assert arrears then eviction.
- Move to a cheaper unit and assert occupant counts.
- Partner with an NPC and assert the merge/split rules.
- Restore and assert the lease and household survive exactly.

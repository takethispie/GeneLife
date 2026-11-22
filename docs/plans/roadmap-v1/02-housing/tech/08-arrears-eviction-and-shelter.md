# H08 — Arrears, eviction and shelter

**Phase:** 5 — Risk
**Depends:** H05, H07
**Estimate:** 2–3 sessions
**Status:** Implemented (V1)

## Goal

Turn persistent non-payment into a graded, recoverable process that ends in eviction only when alternatives are exhausted, and make homelessness a visible, survivable state.

## Context

Without eviction, rent is optional and the economy loses its main household pressure. With instant eviction, a single bad month destroys households and the population collapses. The system needs a ladder.

## Design

### Arrears ladder

| Stage | Trigger | Effect |
|---|---|---|
| Warning | first missed month | notification event, no penalty |
| Late | 2 months | late fee, landlord can refuse renewal |
| Notice | 3 months | formal notice, 1-month cure window |
| Eviction | cure window expired | tenancy terminated, household relocated |

Cure: any payment that clears `RentArrears` before the eviction tick cancels the process.

### Eviction apply

1. Terminate tenancy and clear `HouseholdByUnit`.
2. Try, in order:
   - the cheapest vacant unit the household can afford with current income;
   - a shelter bed with capacity;
   - a shared unit with relatives if capacity allows;
   - homeless (no `Residence`).
3. Charge moving cost if the household can pay; otherwise record a debt claim.
4. Emit `TenantEvicted`, `HouseholdRelocated`, `HouseholdHomeless`.
5. Release the old unit to the market with a condition penalty and a vacancy flag.

Stagger evictions: at most `MaxEvictionsPerCycle` per system pass so a market shock does not evict the whole population in one tick.

### Shelter

- Shelter properties are municipality-owned or charity-owned, funded by the government account.
- Each bed is a `HousingUnit` with `Tier = Shelter`, capacity 1.
- Shelter quality applies the shelter modifier; residents are `Tenure.Sheltered` and pay a nominal fee.
- Shelter capacity is finite. Overflow becomes literal homelessness: no unit, worst modifiers, faster health decline, higher mortality risk.

### Homelessness recovery

A homeless household:

- searches for the cheapest vacant unit every month;
- can be prioritized by an admin housing program;
- regains normal modifiers immediately on move-in.

## Tasks

- [ ] Add notice state and cure deadline to `Household`.
- [ ] Implement the eviction planner and apply phase.
- [ ] Add shelter properties and beds to the seed/admin tools.
- [ ] Implement relocation search with affordability and capacity checks.
- [ ] Add moving-cost ledger entry and debt claim on inability to pay.
- [ ] Emit the eviction/relocation/homeless events.
- [ ] Add `MaxEvictionsPerCycle` and notice-period options.
- [ ] Add admin actions: issue notice, cancel notice, evict now, assign shelter.
- [ ] Expose arrears ageing, notices and shelter occupancy.

## Files

- new `Genelife/Systems/EvictionSystem.cs`
- `Genelife/Components/Housing/Household.cs`
- `Genelife.API/Repositories/HouseholdRepository.cs`
- `Genelife.API/Services/HouseholdAssigner.cs`
- `Genelife.Messages/Events/Tenant*.cs`, `HouseholdRelocated.cs`, `HouseholdHomeless.cs`
- `Genelife.API/Services/SimulationEngine.cs`

## Acceptance

- A household that pays within the cure window is not evicted.
- Eviction frees the unit and relocates the household to the best available option.
- Shelter capacity is respected; overflow produces explicit homelessness.
- Homeless households have the worst modifiers and can recover.
- No more than the configured number of evictions happen per cycle.
- The admin can see and operate every stage of the ladder.

## Testing

- Ladder timing test.
- Cure cancellation test.
- Relocation priority test.
- Shelter overflow test.
- Eviction rate limit test.
- Move-in recovery test.

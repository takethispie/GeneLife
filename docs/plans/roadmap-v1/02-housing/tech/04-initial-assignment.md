# H04 — Initial assignment

**Phase:** 2 — Households
**Depends:** H03
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Place a generated population into households and units in one deterministic pass, including children with adults and a shelter fallback.

## Context

The current world can create arbitrary sims with `SimRepository.Add` and no home. Life stages introduces a population generator; housing must be able to absorb it at world creation and on demand.

## Design

`HouseholdAssigner.Assign(World, HousingRegistry, PopulationPlan, seed)`:

1. Sort adults by age, then `NetworkId`.
2. Form households deterministically:
   - 55% single adult;
   - 30% couple (two adults, similar age band);
   - 15% single parent or multi-adult family.
3. Attach children and students to the youngest adult household with capacity.
4. Compute household income from employment or expected wage.
5. Rank vacant units by affordability:
   - rent ≤ 35% of projected household income;
   - capacity ≥ member count;
   - prefer higher quality within budget.
6. Assign the best available unit; if none, assign a shelter bed; if no shelter, leave homeless with `Residence` unset.
7. Create the tenancy with `Tenure.Renter` or `Tenure.Owner` for wealthy households.
8. Emit `HouseholdFormed` and `TenancyStarted`.

Determinism: use `SimulationRandom.For("housing.assignment")`; never `Random.Shared`.

The same service is used by the admin `POST /api/population/spawn` command and by the population generator in life stages.

## Tasks

- [ ] Add `PopulationPlan` (adults, children, income estimate) shared with life stages.
- [ ] Implement household formation and child attachment.
- [ ] Implement unit ranking and assignment.
- [ ] Add shelter fallback and explicit homeless outcome.
- [ ] Add `POST /api/housing/assign` for a single sim and `POST /api/population/spawn` for a batch.
- [ ] Emit assignment metrics: housed, sheltered, homeless, overcrowded.
- [ ] Add a deterministic seed test.
- [ ] Ensure registries are updated in the apply phase.

## Files

- new `Genelife.API/Services/HouseholdAssigner.cs`
- new `Genelife/PopulationPlan.cs`
- `Genelife.API/Repositories/HouseholdRepository.cs`
- `Genelife.API/Endpoints/Wolverine/HousingEndpoints.cs`
- `Genelife.Messages/Commands/SpawnPopulation.cs`

## Acceptance

- Every generated child belongs to an adult household.
- Assignment is deterministic for a fixed seed and input order.
- No household is placed in a unit below its capacity unless it is explicitly overcrowded and no alternative exists.
- Households with insufficient income fall to cheaper tiers, then shelter, then homeless.
- Registry and `Residence` match after assignment.
- Admin can spawn a population and see the housed/sheltered/homeless split.

## Testing

- Household composition distribution test.
- Affordability ranking test.
- Shelter fallback test.
- Deterministic replay test.
- Empty-stock scenario test.

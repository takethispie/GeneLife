# R05 — Candidate generation and budgets

**Phase:** 5 — Scoring
**Depends:** R02–R04
**Estimate:** 2 sessions
**Status:** Done

## Goal

Produce a small, deterministic candidate set for each eligible sim from local institutions and space, never an all-pairs scan.

## Context

At 2,000 sims, an all-pairs attraction pass is roughly two million pairs per quarter. That is not acceptable inside a fixed-step tick. Candidate generation must be local, capped and explainable.

## Design

### Sources

For a sim, candidates come from:

1. **Household** — other adults in the same household (rare, but valid before a partnership).
2. **Workplace** — other employees of the same company/office.
3. **School** — other students at the same school.
4. **Spatial neighbours** — sims within a configured tile radius via `SpatialIndex`.
5. **Recent arrivals** — immigrants added since the last refresh, so they are not invisible for a quarter.

Do not use a global random sample: it hides the locality rule and makes the score harder to explain.

### Pipeline

```csharp
IReadOnlyList<Entity> CandidatesFor(Entity sim, long tick)
```

1. Gather raw entities from each source.
2. Exclude self, dead, dependants and ineligible ages.
3. Deduplicate by entity.
4. Apply `EligibilityPolicy` and keep the rejection reasons for admin diagnostics.
5. Sort by `NetworkId`.
6. Cap at `RelationshipOptions.MaxCandidatesPerSim`.

### Caching

- `RelationshipExposureSystem` refreshes candidate lists monthly.
- `AttractionSystem` scores the cached lists quarterly.
- A partner change, household move, job change or school change invalidates the affected sim's cache immediately.
- Cache entries are not persisted; they are rebuilt after restore.

### Determinism

- Source queries iterate in stable order.
- The final list is sorted by `NetworkId` before scoring.
- Candidate generation never rolls random numbers.
- Adding a new source must not reorder existing candidates for a fixed world.

### Budgets

- `MaxCandidatesPerSim` default 24.
- At 2,000 sims, the worst case is 48,000 candidate evaluations per quarterly pass, not 2,000,000.
- Metrics record average, p95 and maximum candidate counts.

## Tasks

- [ ] Add `CandidateGenerator` with the five sources.
- [ ] Add dedupe, eligibility filter and stable sort.
- [ ] Add the per-sim cap.
- [ ] Add cache invalidation hooks for partner, household, job and school changes.
- [ ] Add `RelationshipExposureSystem` at order 355, cadence 100.
- [ ] Add candidate-count metrics.
- [ ] Add an all-pairs guard test that fails if the generator scans the population.

## Files

- new `Genelife/Family/CandidateGenerator.cs`
- new `Genelife/Family/CandidateCache.cs`
- new `Genelife/Systems/RelationshipExposureSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Family/CandidateGenerationTests.cs`

## Acceptance

- Every candidate set respects the cap.
- Candidates are deduplicated and sorted by `NetworkId`.
- Ineligible candidates are absent from the scored list and have a recorded reason.
- A partner change invalidates the cache.
- No code path iterates the full living population per sim.
- The 2,000-sim candidate pass stays inside the quarterly budget.

## Testing

- Build a world with shared households, offices, schools and neighbours and assert the union.
- Assert the cap and the stable order.
- Move a sim and assert the neighbour source changes.
- Pair a sim and assert the cache invalidates.
- Instrument the generator and assert the number of inspected entities is sub-quadratic.
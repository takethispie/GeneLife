# L15 — Mortality and death processing

**Phase:** 6 — Elderly
**Depends:** L14, H08
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Close the lifecycle with death from age, health, poverty and unmet needs, and clean up every relationship exactly once.

## Context

Nothing removes sims. Entities, employment, housing, school and pension state would leak forever. Mortality must queue deaths outside queries and coordinate with housing and economy cleanup.

## Design

### Causes

```csharp
public enum DeathCause { Age, Illness, Starvation, Exposure, Other }
```

### Risk model

`MortalitySystem` order 570, cadence 600 (twice per quarter):

```
ageRisk = age < 65 ? 0 : pow((age − 65) / 45, 2) × 0.02
healthRisk = (1 − health / 100) × 0.01
povertyRisk = household cash < oneMonthExpenses ? 0.004 : 0
housingRisk = homeless ? 0.01 : (shelter ? 0.005 : 0)
risk = min(0.15, ageRisk + healthRisk + povertyRisk + housingRisk)
```

- Roll with `SimulationRandom.For("mortality")`.
- `Health == 0` guarantees death at the next check.
- Elders have a higher floor; young healthy sims have near-zero risk.

### Needs death

`NeedsDeathSystem` order 575, cadence 1:

- trigger only if a critical need is 0 for `N` consecutive ticks (default 10 s = 200 ticks);
- no active restoring action;
- no household funds to buy the relevant category;
- not already flagged dead.

This prevents death while eating or while a purchase is pending.

### Death processing

`DeathProcessingSystem` order 576, cadence 1, drains the queue:

1. Mark `Dead { long Tick; DeathCause Cause; }`; sims are skipped by all gameplay queries.
2. Remove from `EmploymentIndex`, `HousingRegistry`, school enrollment, household membership and spatial index.
3. Free the job seat and, if the deceased was the last household member, release the housing unit.
4. Cancel pending job applications and benefits.
5. Emit `SimDied` with age, cause, household, employer and wealth snapshot.
6. Queue `EstateSettlementRequested`.
7. Keep the entity for one snapshot tick, then destroy it in the apply phase.

Destroying the entity removes `NetworkId` from the registry after the death event is emitted.

## Tasks

- [ ] Add `Dead` marker and `DeathCause`.
- [ ] Implement stochastic risk with seeded randomness.
- [ ] Implement needs-depletion death with debounce.
- [ ] Implement a death queue and single-pass cleanup.
- [ ] Emit `SimDied` and queue estate settlement.
- [ ] Remove all indices and relationships exactly once.
- [ ] Add mortality metrics: deaths by cause, average age, average wealth.
- [ ] Add admin actions to force death for scenarios and a mortality log.
- [ ] Ensure no system processes a `Dead` sim.

## Files

- new `Genelife/Components/Gen/Dead.cs`
- new `Genelife/Systems/MortalitySystem.cs`, `NeedsDeathSystem.cs`, `DeathProcessingSystem.cs`
- `Genelife/Systems/NeedsDecaySystem.cs`, `HiringSystem.cs`
- `Genelife.Messages/Events/SimDied.cs`
- `Genelife.API/Services/SimulationEngine.cs`

## Acceptance

- An elder with poor health dies within a plausible number of years.
- A healthy young sim almost never dies.
- A starving, broke, homeless sim dies from needs within the configured window.
- Death removes every relationship exactly once.
- The entity is destroyed after the snapshot tick, not during a query.
- Mortality metrics and log are visible to the admin.

## Testing

- Risk distribution over 1,000 sims.
- Needs-death debounce test.
- Cleanup completeness test (employment, housing, household, school).
- Duplicate-death prevention test.
- Entity destruction timing test.
- Determinism test with a seed.

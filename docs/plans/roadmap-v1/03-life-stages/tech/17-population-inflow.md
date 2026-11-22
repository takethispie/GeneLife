# L17 — Population inflow

**Phase:** 7 — Continuity
**Depends:** L15, H04, E16
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Keep the population and age pyramid viable over decades by admitting working-age immigrants when jobs and housing exist, without minting wealth or distorting the economy.

## Context

V1 has no births. Without inflow, the first generation retires, dies and the world empties. Immigration is the replacement mechanism and a labour-supply regulator.

## Design

`PopulationInflowSystem` order 590, cadence 100 (monthly), with an annual budget reset at the year boundary.

### Triggers

```
labourGap = openSeats − unemployedWorkers
housingGap = vacantAffordableUnits
populationGap = targetPopulation − currentPopulation
pyramidPressure = workingAgeShare < 0.55
```

Inflow occurs when:

- `labourGap > 0` or `populationGap > 0` or `pyramidPressure`;
- `housingGap > 0` or shelter capacity exists;
- unemployment is below the immigration pause threshold (default 10%);
- an annual inflow budget remains.

### Arrivals

Each arrival:

- age 18–45, distribution skewed young;
- skill profile from a distribution matching open roles;
- cash = `ImmigrantStartingCash` (small, default 500 units), ledgered as `Grant`;
- joins or forms a household via `HouseholdAssigner`;
- rents a unit or enters shelter;
- becomes a `JobSeeker`;
- starts pension contributions on first job.

Arrivals never spawn with a company or ownership. They must earn their position.

### Budget and pacing

- Max arrivals per year = `max(5, targetPopulation × 2%)`.
- Spread across the year in monthly batches to avoid a single-tick shock.
- If housing is unavailable, arrivals queue in a `PendingArrivals` pool for up to one year; then they are cancelled rather than made homeless en masse.

### Emigration (optional)

If unemployment is high and housing is expensive for a sustained period, a small number of working-age households may leave. V1 can ship without emigration; add it only if the population overshoots.

## Tasks

- [ ] Add triggers, budget and pacing.
- [ ] Implement arrival generation with skills, cash and household assignment.
- [ ] Add `PendingArrivals` with expiry.
- [ ] Emit `ImmigrantArrived`, `ImmigrationPaused`, `ArrivalCancelled`.
- [ ] Add starting-cash grant to the ledger.
- [ ] Expose inflow, pending, cancelled and pyramid metrics.
- [ ] Add admin controls: set target, trigger wave, pause/resume.
- [ ] Add a scenario test for a population crash and recovery.

## Files

- new `Genelife/Systems/PopulationInflowSystem.cs`
- new `Genelife/ImmigrationPolicy.cs`
- `Genelife.API/Services/HouseholdAssigner.cs`
- `Genelife.API/Repositories/SimRepository.cs`
- `Genelife.Messages/Events/ImmigrantArrived.cs`
- `Genelife.API/Endpoints/Wolverine/PopulationEndpoints.cs`

## Acceptance

- A population crash triggers inflow and recovers toward target.
- No arrival is created without a housing or shelter path.
- Starting cash is ledgered and does not break conservation.
- Inflow pauses when unemployment is high.
- Arrivals get jobs and pay taxes within a few cycles.
- Admin can trigger and pause inflow.

## Testing

- Trigger matrix.
- Pacing and budget test.
- Pending arrival expiry test.
- Ledger grant test.
- Population recovery scenario.

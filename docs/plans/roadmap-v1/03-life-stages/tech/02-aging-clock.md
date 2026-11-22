# L02 — Aging clock

**Phase:** 1 — Foundation
**Depends:** L01, E02
**Estimate:** 1–2 sessions
**Status:** Implemented (V1)

## Goal

Advance age deterministically from simulation ticks so every lifecycle transition has a reliable clock.

## Context

`Alive.Age` is set at creation and never changes. `SimulationClock.TicksPerYear` is 1,200 by default. Age must advance incrementally but remain cheap at thousands of sims.

## Design

`AgingSystem` order 90, cadence 20 (once per second):

```csharp
world.Query(in query, (ref Lifecycle life, ref Alive alive) =>
{
    life.AgeTicks += ticksSinceLastRun;
    var newAge = (int)(life.AgeTicks / clock.TicksPerYear);
    if (newAge != alive.Age)
        alive.Age = newAge;
});
```

- `ticksSinceLastRun` is derived from `context.Tick` and a stored `LastAgedTick`, so skipped cadences do not lose time.
- `Alive.Age` remains the integer public age used by existing code and DTOs.
- `AgeTicks` is authoritative for boundaries and precise decline.
- At `MaxAge`, the sim is flagged for mortality; age does not exceed the cap.
- New sims receive `AgeTicks = age × TicksPerYear + phaseOffset`, with a deterministic phase offset so birthdays are spread across the year.

No structural changes and no events per tick. Emit `SimAged` only when the integer age changes.

## Tasks

- [ ] Add `AgeTicks` and `LastAgedTick` handling.
- [ ] Implement `AgingSystem` with cadence-safe accumulation.
- [ ] Spread birthdays using a deterministic phase offset.
- [ ] Emit `SimAged` on integer age change.
- [ ] Clamp at `MaxAge` and emit `MaxAgeReached`.
- [ ] Update all sim creation paths to initialise `AgeTicks` from `Age`.
- [ ] Persist and restore `AgeTicks`.
- [ ] Add an admin control to advance a sim by N years for testing.

## Files

- new `Genelife/Systems/AgingSystem.cs`
- `Genelife/Components/Life/Lifecycle.cs`, `Genelife/Components/Gen/Alive.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Repositories/SimRepository.cs`
- `Genelife.Messages/Events/SimAged.cs`

## Acceptance

- After `TicksPerYear` ticks, every sim's age increases by exactly one.
- Ages advance correctly when cadence passes are skipped or the loop lags.
- Birthday offsets remain stable across save/restore.
- No sim exceeds `MaxAge`.
- Aging adds no measurable allocation per sim.
- Existing code that reads `Alive.Age` keeps working.

## Testing

- Year-boundary test for one and many sims.
- Skipped-tick accumulation test.
- Phase offset distribution test.
- Save/restore age ticks test.
- Performance test at 2,000 sims.

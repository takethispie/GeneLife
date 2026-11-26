# Cadence Phase Staggering

## Context

`Cadence.ShouldRun` is `tick % Interval == 0` (`Genelife.Application/Systems/ISimulationSystem.cs:20`). Every monthly system (`RelationshipExposure`, `Conception`, `Birth`, `FamilyHousehold`, `StateCare`, `Trust`, `Pregnancy`) fires on the same tick, as do pay-cycle systems. That produces a periodic spike each month — a thundering herd.

## Design

Add an optional per-system offset:

```csharp
public bool ShouldRun(long tick) => (tick + Offset) % Interval == 0;
```

- Default `Offset = 0` preserves current behavior.
- Assign offsets to monthly and pay-cycle systems so their work is spread across the interval.
- Keep yearly and metric systems on their existing phase unless they also spike.

## Tasks

- [ ] Add an optional `Offset` to `Cadence`.
- [ ] Assign offsets to the monthly and pay-cycle systems.
- [ ] Confirm outcomes are unchanged (same number of runs per interval, different tick).
- [ ] Run the 2k decade and record the per-tick p99 distribution.

## Files

- `Genelife.Application/Systems/ISimulationSystem.cs`
- `Genelife.Application/Systems/SystemRegistry.cs`
- Affected system registrations in `Genelife.Application/Systems`

## Contracts

- Each system still runs once per interval.
- Only the tick on which it runs changes.

## Acceptance

- Monthly systems no longer all fire on the same tick.
- The periodic overrun spike is gone or reduced.
- Balance and determinism tests pass; per-interval run counts are unchanged.

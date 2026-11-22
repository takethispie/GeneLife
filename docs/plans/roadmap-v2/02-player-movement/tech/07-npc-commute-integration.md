# P07 — NPC commute integration

**Phase:** 4 — Integration
**Depends:** P04, P06, M06
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Connect work and school presence to real building entries without making every NPC a per-tick pathfinder.

## Context

`WorkScheduleSystem` (`Genelife/Systems/WorkScheduleSystem.cs`) starts and ends shifts, changes `CurrentAction` to/from `Working`, and tracks attendance. It does not update `Location`, despite the V1 roadmap describing location changes. All NPCs therefore remain at the spawn point.

`WorkSchedule` stores the office entity. Home is available through `Residence` and `HousingRegistry`.

## Design

### Shift start

For a non-player-controlled employee starting a shift:

1. resolve the office placement;
2. set `TilePosition` to the office entry;
3. derive `Location`;
4. update `SpatialIndex`;
5. set `CurrentAction = Working`;
6. keep attendance and performance counters unchanged.

Player-controlled sims are already excluded by `WorkScheduleSystem`; keep that rule.

### Shift end

1. resolve the sim's residence entry;
2. if homeless, use the shelter entry when available;
3. if neither exists, use the deterministic map fallback spawn;
4. set `TilePosition`/`Location`;
5. set `CurrentAction = Idle`.

### Interruptions

When a critical need interrupts a shift:

- end the shift as today;
- move the sim to its home entry if the interrupting action is sleeping, eating or hygiene;
- otherwise leave it at the office entry;
- record the interruption for attendance.

### School presence

Apply the same entry rule to enrolled students when a school day starts or ends, if V2 already has a school schedule. If V1 has no explicit school-day clock, V2 may restrict this to work and leave school commutes to a later step; the entry contract is already in place.

### Optional visible movement

Default V2 behaviour is entry teleport at shift boundaries. A configurable `CommuteMode` may allow visible NPCs to be animated:

```csharp
public enum CommuteMode
{
    Abstract = 0,
    AnimateVisible = 1
}
```

`AnimateVisible` uses `MoveTarget` and P05 pathfinding only for NPCs inside an observer's viewport. It must be disabled by default until the performance budget proves it.

### Determinism

- Shift boundaries are tick-based.
- Entry resolution is stable by building network id.
- Fallback spawn search uses the same stable tile order as P01.
- No random destination is chosen for NPCs.

## Tasks

- [ ] Resolve office/home/shelter entries in `WorkScheduleSystem`.
- [ ] Write `TilePosition` and derived `Location` at shift boundaries.
- [ ] Keep player-controlled sims out of automatic commuting.
- [ ] Add interruption destination rules.
- [ ] Add optional visible-NPC commute mode behind configuration.
- [ ] Add `CommuteStarted`/`CommuteCompleted` events if needed for the UI.
- [ ] Add tests for shift start/end, homeless fallback, interruption and player exclusion.
- [ ] Confirm no per-tick pathfinding is added for all NPCs.

## Files

- `Genelife/Systems/WorkScheduleSystem.cs`
- `Genelife/Components/Life/WorkSchedule.cs`
- `Genelife/Map/PlayerSpawnService.cs` (shared entry resolution)
- `Genelife/Map/CommuteOptions.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Systems/WorkScheduleTests.cs`

## Acceptance

- A worker starts a shift at the office entry and ends it at the home entry.
- A homeless worker uses a shelter or deterministic fallback.
- Interrupted shifts apply the destination rule.
- Player-controlled sims are never moved by the schedule.
- Default mode performs no per-tick pathfinding for NPCs.
- Entry teleports occur at shift boundaries, not every tick.
- Save/restore preserves the post-teleport position.

## Testing

- Shift start/end entry resolution.
- Homeless/shelter fallback.
- Interruption destination matrix.
- Player exclusion.
- No-pathfinding assertion in abstract mode.
- Determinism across repeated runs.

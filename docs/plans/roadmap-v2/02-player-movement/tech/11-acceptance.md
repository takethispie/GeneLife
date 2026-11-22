# P11 — Acceptance

**Phase:** 8 — Verification
**Depends:** P01–P10
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Prove player presence and movement are server-authoritative, collision-safe, deterministic, durable and usable at the multiplayer target.

## Context

Movement can look correct in a single-sim test while failing under disconnected clients, reconnects, blocked routes, mid-route saves or concurrent spawns. Acceptance must exercise the full path from hub intent to snapshot.

## Acceptance scenarios

### Spawn

1. Assign a sim with a residence to a player.
2. Join and assert the sim is at the property entry.
3. Repeat for a homeless sim and assert the deterministic fallback.
4. Spawn two players simultaneously and assert distinct tiles.
5. Reconnect and assert the position is unchanged.

### Intent and authority

1. Submit a valid move intent.
2. Assert it is admitted and applied on the next tick.
3. Submit a spoofed `PlayerId` and assert `NotOwned`/`PlayerMismatch`.
4. Submit an out-of-bounds target and assert `OutOfBounds`.
5. Submit a blocked target and assert `TargetBlocked`.
6. Submit a duplicate sequence and assert no double movement.
7. Submit a float target and assert the contract rejects it.

### Movement and collision

1. Move a sim along a route around a building.
2. Assert it never occupies an occupied footprint.
3. Assert it stops at the entered building's entry.
4. Place a building on a route and assert the route is invalidated.
5. Drive movement at the map edge and assert no out-of-bounds tile.

### Interactions

1. Arrive at an office entry and use it while employed.
2. Attempt use from several tiles away and assert `NotInRange`.
3. Attempt office use without employment and assert `NotEmployedHere`.
4. Assert a player-controlled sim never auto-uses a building.

### NPC commute

1. Start and end a shift and assert office/home entries.
2. Assert a homeless worker falls back to shelter/spawn.
3. Assert player-controlled sims are not auto-moved.
4. Assert abstract mode performs no per-tick pathfinding.

### Persistence

1. Save mid-route.
2. Restore and assert the same tile/target or a clearly defined last-valid-tile state.
3. Assert the target is re-pathed.
4. Continue movement and compare invariants.
5. Assert no route array is in the save.

### Multiplayer

1. Connect 32 clients.
2. Give each a controlled sim and a valid spawn.
3. Send concurrent movement intents.
4. Assert every client receives consistent positions for visible sims.
5. Assert reconnect resumes from the acknowledged tick and still receives movement deltas.

### Determinism

1. Run the same seed and intent script twice.
2. Assert identical final tile positions and path lengths.
3. Assert no wall-clock time affects movement.

## Tasks

- [ ] Add the spawn and reconnect scenarios.
- [ ] Add the intent/authority matrix.
- [ ] Add collision/path invalidation scenarios.
- [ ] Add interaction and NPC commute scenarios.
- [ ] Add mid-route save/restore.
- [ ] Add the 32-client movement scenario.
- [ ] Add the deterministic intent script.
- [ ] Record the result in `roadmap-v2/STATUS.md`.

## Files

- `Genelife.UnitTests/Map/PlayerSpawnTests.cs`
- `Genelife.UnitTests/Commands/IntentDrainTests.cs`
- `Genelife.UnitTests/Systems/MovementSystemTests.cs`
- `Genelife.UnitTests/Systems/BuildingInteractionTests.cs`
- `Genelife.UnitTests/Persistence/MovementPersistenceTests.cs`
- `Genelife.UnitTests/Performance/MultiplayerAcceptanceTests.cs`
- `docs/plans/roadmap-v2/STATUS.md`

## Acceptance

- All scenarios pass on a clean build.
- No client-supplied position is accepted as authoritative.
- No movement passes through a building footprint or map edge.
- Reconnect preserves position and resumes deltas.
- Save/restore resumes movement without path serialization.
- Abstract NPC commuting stays off the pathfinding hot path.
- The 32-player scenario passes within the V1 tick and bandwidth budgets.

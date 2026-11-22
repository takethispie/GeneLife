# R09 — Scenario tests

**Phase:** 7 — Verification
**Depends:** R02–R08
**Estimate:** 2–3 sessions
**Status:** Planned (V2)

## Goal

Exercise the complete V2 path in realistic scenarios: generated map, building placement, player spawn, movement, viewport changes, reconnect, save/restore and a live map mutation while clients are connected.

## Context

Unit tests can pass while the composition fails: the map page reads a stale version, an intent is applied before a building exists, a delta misses a newly visible sim, or a save restores the map but not the movement target. Scenario tests catch composition errors.

## Scenarios

### Fresh world with connected clients

1. Start a world with a fixed seed and initial stock.
2. Connect several clients and assign controlled sims.
3. Assert each client receives a map snapshot and a full world snapshot.
4. Assert each player has a valid spawn.
5. Move each player and assert positions converge on the server.

### Map change while connected

1. Place a new building through the admin command path.
2. Assert connected clients receive a `MapDelta` with the new version.
3. Assert a building removal is sent as a removal.
4. Assert a client with an old version gets a full map.
5. Assert placement did not block a player without invalidating the route correctly.

### Viewport enter/leave

1. Set a small viewport around a player.
2. Move another sim into and out of the viewport.
3. Assert the client receives an addition and then a removal.
4. Assert the player's own sim remains visible outside the radius.
5. Compare bytes with and without AOI.

### Reconnect

1. Move a player and acknowledge a tick.
2. Disconnect.
3. Move the world forward.
4. Reconnect and request from the acknowledged tick.
5. Assert a delta resumes and the map version is current.
6. Assert the player position matches the server.

### Save, restore and continue

1. Run a scenario with placements, players and mid-route movement.
2. Save and stop.
3. Restore.
4. Assert map, placements, positions and targets.
5. Continue and compare with a continuous run.

### Stopped simulation

1. Stop the simulation.
2. Load `/api/map` and the admin map page.
3. Assert the last map and positions are still readable and clearly marked as stopped.
4. Assert no broadcast or command is applied until restart.

## Tasks

- [ ] Build a scenario harness with a fixed seed and scripted commands/intents.
- [ ] Add the connected-client map/world scenario.
- [ ] Add the live map-change scenario.
- [ ] Add the AOI enter/leave scenario.
- [ ] Add the reconnect scenario.
- [ ] Add save/restore/continue.
- [ ] Add stopped-simulation read tests.
- [ ] Record payload and timing baselines.

## Files

- `Genelife.UnitTests/Performance/MultiplayerAcceptanceTests.cs`
- new `Genelife.UnitTests/Map/MapScenarioTests.cs`
- `Genelife.UnitTests/Snapshots/MapReplicationTests.cs`
- `Genelife.UnitTests/Persistence/MapPersistenceTests.cs`
- test fixtures and seeded scripts under `Genelife.UnitTests/Fixtures`

## Acceptance

- All scenarios pass in CI with PostgreSQL available.
- No scenario requires direct `World` access from a client.
- Map changes and movement updates remain consistent across clients.
- AOI reduces bytes without losing the player's own sim.
- Reconnect resumes from an acknowledged state.
- Restore continues without divergence.
- Stopped simulation remains readable.

## Testing

- Full scenario suite.
- Payload-size assertions.
- Timing and allocation baselines.
- Multi-client consistency assertion.
- Deterministic replay assertion.

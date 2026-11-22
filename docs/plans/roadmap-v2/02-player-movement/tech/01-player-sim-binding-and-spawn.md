# P01 — Player sim binding and spawn

**Phase:** 1 — Foundation
**Depends:** V1, M05, M06
**Estimate:** 2 sessions
**Status:** Planned (V2)

## Goal

Bind a player identity to exactly one controlled sim and give that sim a valid, deterministic spawn tile without mutating the world from the SignalR connection thread.

## Context

`PlayerControlled` stores a `PlayerId` (`Genelife/Components/Gen/PlayerControlled.cs`). `SimulationEngine.SetOwner` attaches or updates it, `GetOwnerPlayerId` resolves ownership, and `CommandAuthorizationService` uses that for commands. `WorldHub.OnConnectedAsync` registers the connection and sends a snapshot, but does not ensure the player has a sim in a valid place.

Every sim creation path writes `(0,0)`. After the map exists, `(0,0)` may be a building, water or another player.

## Design

### Binding index

Add a derived `PlayerBindingIndex`:

```csharp
public sealed class PlayerBindingIndex
{
    public IReadOnlyDictionary<string, Entity> SimByPlayer { get; }
    public IReadOnlyDictionary<Entity, string> PlayerBySim { get; }

    public void OnOwnerChanged(Entity sim, string playerId);
    public void OnRemoved(Entity sim);
    public void Rebuild(World world);
}
```

Rules:

- one active sim per player in V2;
- one player per controlled sim;
- duplicate `PlayerId` on two sims is a reconciliation error;
- the index is rebuilt after restore and updated only in apply phases.

`PlayerControlled` remains the authoritative component. The index is a hot-read cache, like `EmploymentIndex`.

### Spawn service

```csharp
public sealed class PlayerSpawnService
{
    public SpawnResult ResolveSpawn(Entity sim);
}
```

Resolution order:

1. `HousingRegistry.TryGetUnitForSim` → `HousingUnit.Property` → `Placement.Entry`.
2. If the sim has no residence, use the nearest deterministic `GeneratedMap.SpawnPoints` entry.
3. If the chosen tile is blocked or claimed, search outward in stable tile order for the nearest walkable free tile.
4. If none exists, fail with `NoSpawnTile`.

"Claimed" means another player's controlled sim already occupies that tile or is in the process of spawning during the same tick. NPCs may share a tile in V2; players should not stack at spawn.

### Join path

The hub does not write the world. It enqueues a command or intent:

```csharp
public sealed record EnsurePlayerPresenceIntent : Intent
{
    public ulong SimNetworkId { get; init; }
}
```

At tick start:

1. resolve the sim by network id;
2. verify the player owns it;
3. if it already has a valid `TilePosition`, leave it alone;
4. otherwise resolve spawn and write `TilePosition` + derived `Location`;
5. update the binding index;
6. emit `PlayerSpawned`.

Reconnection reuses the sim and does not relocate it unless the saved position is invalid.

### Multiple players

Spawn requests are processed serially at tick start. Ties are settled by stable order: command sequence, then sim network id. A spawned player is always included in its own replication view even if other players share the area.

## Tasks

- [ ] Add `PlayerBindingIndex` and rebuild/reconcile support.
- [ ] Add `PlayerSpawnService` with residence and fallback resolution.
- [ ] Add `EnsurePlayerPresenceIntent`/command and tick-start handling.
- [ ] Add `PlayerSpawned` and `PlayerSpawnRejected` events.
- [ ] Reuse an existing valid position on reconnect.
- [ ] Add a spawn-claim mechanism for simultaneous joins.
- [ ] Add tests for residence, fallback, blocked entry, no free tile and duplicate ownership.
- [ ] Expose player binding in the admin read model.

## Files

- new `Genelife/Map/PlayerSpawnService.cs`, `PlayerBindingIndex.cs`
- new `Genelife.Messages/Events/PlayerSpawned.cs`
- `Genelife.API/Commands/Intent.cs`
- `Genelife.API/Hubs/WorldHub.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Map/PlayerSpawnTests.cs`

## Acceptance

- A player with a residence spawns at the property entry when free.
- A player without a residence spawns at a deterministic map spawn point.
- A blocked spawn resolves to the nearest free walkable tile.
- A valid reconnecting sim is not moved.
- Two simultaneous spawns do not occupy the same tile.
- Spawn mutation happens at tick start, not inside `OnConnectedAsync`.
- Duplicate player bindings are reported by reconciliation.

## Testing

- Residence-entry spawn.
- No-residence fallback.
- Blocked/occupied spawn search.
- Reconnect no-move test.
- Simultaneous spawn test.
- Duplicate binding reconciliation.
- Ownership rejection test.

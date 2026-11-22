# Player Movement — Summary

**Folder:** `02-player-movement/`
**Status:** Implemented (V2)

**Depends on:** world map placement and bootstrap (`01-world-map/tech/05`, `06`), existing `PlayerControlled`, `MoveIntent`, `ICommandQueue`, `CommandAuthorizationService`, `SimulationEngine` tick loop
**Feeds:** map replication, admin map console and future gameplay clients

## Objective

Make a player-controlled sim a real presence in the world: it spawns at a valid place, accepts movement intents, walks through the map under server authority, respects buildings, and replicates its position to clients. NPC commuting should use the same tile map without turning every NPC into a per-tick pathfinder.

## Baseline before V2

- `PlayerControlled { PlayerId }` exists and is used by `DecisionSystem` to skip autonomous decisions.
- `MoveIntent { PlayerId, X, Y }` exists and `WorldHub.SubmitIntent` enqueues it.
- `ICommandQueue` is registered in `Program.cs` but nothing drains it. The wave-5 results list this as an open item.
- `CommandAuthorizationService` validates ownership for `SimCommand`s, but the intent path does not yet convert intents into those commands.
- There is no `MovementSystem`, no pathfinding and no arrival/interaction handling.
- Every sim is created at `(0,0)` with no valid spawn logic.
- `WorkScheduleSystem` changes action state but does not move `Location`.

## Deliverables

1. Player-to-sim binding and deterministic spawn resolution.
2. Intent queue drain at tick start.
3. Movement components and a derived `MovementIndex`.
4. `MovementSystem` with tile stepping, bounds and building collision.
5. Budgeted A* pathfinding for player-controlled and visible sims.
6. Arrival handling and building-entry interactions.
7. NPC commute integration with building entries.
8. Stable intent wire contract and authorization tests.
9. Movement persistence and path-cache rebuild.
10. Movement metrics and performance guardrails.
11. Acceptance scenarios for spawn, movement, collision, restore and multiplayer.

## Architecture

```
WorldHub.SubmitIntent
        │
        ▼
   ICommandQueue
        │  drained at tick start
        ▼
IntentDispatcher ──► CommandAuthorizationService
        │
        ▼
   MoveTarget (ECS)
        │
        ├── PathfindingSystem (35, budgeted)
        ▼
   MovementSystem (40)
        │
        ├── WorldMap collision
        ├── TilePosition + derived Location
        └── SpatialIndex / MovementIndex
                │
                ▼
       WorldSnapshot / Delta
```

Player position authority lives entirely on the server. The client sends a target tile and interpolates the returned positions.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Player sim binding and spawn](tech/01-player-sim-binding-and-spawn.md) | Foundation |
| 02 | [Intent queue drain](tech/02-intent-queue-drain.md) | Commands |
| 03 | [Movement domain model](tech/03-movement-domain-model.md) | Foundation |
| 04 | [Movement system and collision](tech/04-movement-system-and-collision.md) | Movement |
| 05 | [Pathfinding](tech/05-pathfinding.md) | Movement |
| 06 | [Arrival interactions and building entry](tech/06-arrival-interactions-and-building-entry.md) | Movement |
| 07 | [NPC commute integration](tech/07-npc-commute-integration.md) | Integration |
| 08 | [Intent contract and authorization](tech/08-intent-contract-and-authorization.md) | Security |
| 09 | [Movement persistence and restore](tech/09-movement-persistence-and-restore.md) | Durability |
| 10 | [Movement metrics and guardrails](tech/10-movement-metrics-and-guardrails.md) | Observability |
| 11 | [Acceptance](tech/11-acceptance.md) | Verification |

## Business reading

- [Server-authoritative movement](business/01-server-authoritative-movement.md)
- [Commuting and pathfinding](business/02-commuting-and-pathfinding.md)
- [Player presence and spawning](business/03-player-presence-and-spawning.md)

## Definition of done

- Every player is bound to exactly one controlled sim.
- A joining player spawns at a residence entry or a deterministic walkable fallback.
- Intents are drained, authorized and applied at tick start.
- Movement is server-authoritative and cannot teleport or pass through buildings.
- Pathfinding is budgeted, deterministic and limited to where it is needed.
- Arrival at a building entry enables the relevant interaction.
- NPC shift boundaries use building entries without per-tick pathfinding for every NPC.
- Save/restore preserves position and movement targets and rebuilds routes.
- Movement metrics and rejections are visible to operators.

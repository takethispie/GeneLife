# Genelife.Domain — domain model

The domain layer of the GeneLife simulation: the Arch ECS components, value
types, policies, indices and deterministic primitives that model the world.
It depends only on `Genelife.Messages` — never on Application, Infrastructure,
API or persistence.

> **Simulation systems do not live here.** Any class implementing
> `ISimulationSystem` belongs to the application layer
> (`Genelife.Application/Systems`, namespace `Genelife.Application.Systems`).
> Domain holds the *model* those systems operate on, plus the pure helpers the
> model itself needs.

## Layout

| Area | Contents |
|---|---|
| `Components/` | Arch ECS components grouped by domain: `Company`, `Consumption`, `Employment`, `Family`, `Finance`, `Gen`, `Housing`, `Life`, `Map`, `Player`, `Social` |
| `Economy/` | `EconomyContext`, money, company accounts, ledger |
| `Housing/` | Households, housing registry, `HouseholdLookup` |
| `Family/` | Traits, relationship graph, attraction/reproduction policies, `NewbornFactory` and indices — the model side; the family systems that drive it live in Application |
| `Life/` | Life stages, lifecycle policies, `HealthPolicy` |
| `Map/` | Tiles, pathfinding (`TilePathfinder`, `MovementOptions`), `SpatialIndex` |
| `Appearance/`, `Consumption/`, `Player/`, `Social/`, `Values/`, `Enums/` | Appearance descriptors, nutrition/pantry model, player state, social graph, value types (`Money`, `NetworkId`), enums |
| `SimulationClock.cs`, `SimulationRandom.cs`, `PopulationPlan.cs` | Deterministic clock, named RNG streams, population plan |

## Determinism

`Random.Shared` is banned. Use `SimulationRandom` named streams and the
`SimulationClock`; keep iteration order stable. Persist ticks and `NetworkId`,
never entity handles.

## Related layers

- `Genelife.Application` — simulation systems, the engine, loop, services and read models.
- `Genelife.Infrastructure` — Marten/PostgreSQL persistence, projections, event writing.
- `Genelife.API` — HTTP and SignalR surface.

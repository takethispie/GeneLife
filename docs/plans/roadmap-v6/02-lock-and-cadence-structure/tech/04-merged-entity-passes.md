# Merged Entity Passes

## Context

`DecisionSystem`, `NeedsDecaySystem`, `ActionSystem`, `MovementSystem`, `PathfindingSystem`, `NeedsDeathSystem` and `VenuePresenceSystem` all run `Cadence.EveryTick` and each walks the world. Several do repeated queries for the same entities.

`DecisionSystem` also does `world.Has<WorkSchedule>` then `world.Get<WorkSchedule>` per entity (`Genelife.Application/Systems/DecisionSystem.cs:38-39`).

## Design

- Merge compatible every-tick passes where the read/write sets do not conflict.
- Restructure `DecisionSystem` to a query that includes `WorkSchedule` optionally, avoiding the double lookup.
- Use Arch chunk iteration to cut repeated query overhead.

Do not merge systems that create, add, remove or destroy entities. The collect → decide → apply rule stays in force.

## Tasks

- [ ] Identify every-tick systems with compatible read/write sets.
- [ ] Merge the safe passes and restructure `DecisionSystem`.
- [ ] Confirm no entity creation/removal inside query callbacks.
- [ ] Run the 2k decade and record per-system time and total tick time.

## Files

- `Genelife.Application/Systems/DecisionSystem.cs`
- `Genelife.Application/Systems/NeedsDecaySystem.cs`
- `Genelife.Application/Systems/SystemRegistry.cs`

## Contracts

- Simulation outcomes are unchanged.
- System ordering rules are preserved.

## Acceptance

- Full-world iterations per tick drop.
- The 2k decade stays inside the budget with the same outcomes.
- Replay and determinism tests pass.

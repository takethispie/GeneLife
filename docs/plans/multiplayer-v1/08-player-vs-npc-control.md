# Step 08 — Player vs NPC control

**Depends:** 07
**Goal:** Stop autonomous AI from overriding player-controlled Sims.

**Status:** Done

## Context

`DecisionSystem` acts on every entity with `Needs + CurrentAction`, so it will fight player input.

## Tasks

- [x] Add `PlayerControlled { PlayerId }` or a `ControllerKind` component.
- [x] Assign it at spawn based on who owns the Sim.
- [x] Make `DecisionSystem` skip player-controlled entities.
- [x] Drive player Sims from intents/actions instead.
- [x] Define default idle behavior for player Sims.
- [x] Keep needs decay and action execution applied to all Sims.

## Files likely touched

- new `Genelife/Components/Gen/PlayerControlled.cs`
- `Genelife/Systems/DecisionSystem.cs`, `ActionSystem.cs`
- `Genelife.Api/Repositories/SimRepository.cs`

## Acceptance

- Player Sims are never auto-decided by AI.
- NPCs retain existing autonomous behavior.
- Needs still decay for players.

## Testing

- Player Sim at critical hunger stays idle until commanded.
- NPC at critical hunger auto-eats.

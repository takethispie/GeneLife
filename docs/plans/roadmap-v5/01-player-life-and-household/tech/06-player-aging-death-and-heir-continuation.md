# P06 — Player aging, death and heir continuation

**Phase:** 6 — Lifecycle
**Depends:** P01–P05, V1 `AgingSystem`, `MortalitySystem`, `DeathProcessingSystem`, `EstateSettlementSystem`
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Make player death a real lifecycle event with a settled estate and a defined continuation path, instead of a silent respawn or a broken session.

## Context

NPCs age, decline, die, settle debts and transfer or escheat an estate. V4 inheritance follows relationship edges and supports minor trusts. Player sims are excluded from autonomous decisions but not from aging or mortality.

Today a player sim that dies would leave the player without a controlled sim. There is no continuation policy, no estate handoff and no way to keep the family story.

## Design

### Continuation policy

```csharp
public enum PlayerContinuationPolicy : byte
{
    Heir,
    NewSim,
    Observer
}
```

- `Heir`: the player continues as an eligible living heir in the same world.
- `NewSim`: a new adult sim is created through the normal population creation path.
- `Observer`: the player stays connected but controls no sim until the next life is available.

The world option defaults to `Heir`. The player can choose a preference, but the server enforces eligibility.

### Death sequence

1. Mortality or health marks the player sim dead.
2. `PlayerControlled` is removed from the dead sim.
3. `DeathProcessingSystem` ends active partnerships and selects a guardian where needed.
4. `EstateSettlementSystem` pays debts, funeral costs, heirs and escheat.
5. The player session enters a `AwaitingContinuation` state.
6. The continuation policy selects an heir or creates a new sim.
7. `PlayerControlled` is added to the new sim and the hub mapping is updated.
8. The player receives a continuation result with the new sim id and a summary of the estate.

### Heir eligibility

- Living partner first, then children, then parent or sibling, following the V4 `HeirResolver` order.
- The heir must not already be player-controlled by another player.
- A minor heir is allowed only if the V5 policy supports guardian-controlled play; otherwise the next adult heir is chosen.
- The player can choose among eligible heirs, but cannot invent one.

### Estate and debts

- The player's wallet, savings, property and debts settle through the existing estate waterfall.
- A player heir receives the inheritance through the normal trust/transfer path.
- A player cannot avoid debts by disconnecting or choosing `NewSim`.
- The estate emits the same events as an NPC estate.

### Observer and disconnect

- `Observer` keeps the player connected with world view but no controlled sim.
- A disconnected player who dies follows the world policy on reconnect.
- A dead player sim is never silently reused.

## Tasks

- [ ] Add `PlayerContinuationPolicy` and per-player preference.
- [ ] Add the `AwaitingContinuation` session state.
- [ ] Remove `PlayerControlled` on death and update the hub mapping.
- [ ] Reuse `EstateSettlementSystem` for the player estate.
- [ ] Add heir selection and the `NewSim` creation path.
- [ ] Add continuation events and read model.
- [ ] Add admin actions for safe continuation and observer mode.
- [ ] Add tests for heir, new sim, observer and debt settlement.

## Files

- new `Genelife/Player/PlayerContinuationPolicy.cs`
- new `Genelife/Player/PlayerContinuationService.cs`
- `Genelife/Systems/DeathProcessingSystem.cs`
- `Genelife/Systems/EstateSettlementSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Hubs/WorldHub.cs`
- `Genelife.UnitTests/Player/PlayerContinuationTests.cs`

## Acceptance

- A player sim ages and can die under the same health and mortality rules as an NPC.
- On death, `PlayerControlled` is removed and the estate settles exactly once.
- `Heir` continues as an eligible living heir with the inherited estate.
- `NewSim` creates a valid adult sim through the normal creation path.
- `Observer` leaves the player connected with no controlled sim.
- A player cannot avoid debts by disconnecting or choosing a new sim.
- The hub mapping and read model point at the new sim after continuation.
- The admin can inspect the death, estate and continuation result.

## Testing

- Age a player sim to death with a living partner; assert partner continuation and estate.
- Repeat with no partner and a living child; assert child continuation.
- Die without an heir; assert the configured fallback.
- Die with debts; assert the estate waterfall and no money creation.
- Disconnect during death; reconnect and assert the policy applies once.
- Assert the dead sim cannot receive player commands.
- Replay a death/continuation sequence and compare state.

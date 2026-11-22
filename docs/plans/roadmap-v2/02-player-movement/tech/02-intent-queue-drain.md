# P02 — Intent queue drain

**Phase:** 2 — Commands
**Depends:** P01
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Drain player intents at tick start, convert them into authorized commands and apply them before systems run.

## Context

`WorldHub.SubmitIntent` validates the payload, checks the `PlayerId` claim, rate-limits and calls `ICommandQueue.TryEnqueue` (`Genelife.API/Hubs/WorldHub.cs`). `CommandQueue` is a bounded channel with `Drain(Action<Intent>)`.

The queue is never drained. `SimulationEngine.Tick` drains legacy `queuedCommands` and `adminCommands`, but not player intents. The wave-5 results explicitly list this as an open item.

## Design

### Tick order

`SimulationEngine.Tick` becomes:

```csharp
DrainAdminCommands();     // map placement and other admin mutations
DrainPlayerIntents();     // movement, presence, interactions
DrainCommands();          // legacy queue; retained during migration
systems.Update(...);
```

Admin commands run first so a building placed in this tick is available to intents in the same tick. The engine and dispatcher share the simulation write lock.

### Dispatcher

```csharp
public sealed class PlayerIntentDispatcher
{
    public void Drain(ICommandQueue queue, World world, long tick);
}
```

For each intent:

1. resolve the controlled sim and player;
2. verify ownership using `CommandAuthorizationService`;
3. convert to a typed command (`MoveCommand`, `InteractCommand`, `EnsurePlayerPresenceIntent`, ...);
4. validate the command-specific payload;
5. apply it;
6. record the result for the player.

The dispatcher never trusts `PlayerId` alone. It resolves the sim from `Intent.TargetNetworkId` (or the bound sim for presence) and compares the owner.

### Intent identity

`Intent` gains:

```csharp
public string PlayerId { get; init; }
public long Sequence { get; init; }
```

`Sequence` is monotonic per connection/player. The dispatcher keeps a small per-player high-water mark:

- duplicate or older sequence: drop;
- gap: accept, but do not require ordering across reconnects;
- reconnect: reset sequence with the new connection and log the transition.

The existing `MessageId` pattern on `EnqueuedCommand` can be reused if a stable command id is preferred.

### Results

`WorldHub.SubmitIntent` already returns `IntentResult` for admission. Add async rejection feedback:

- `IntentAccepted(sequence, tick)` is optional; the snapshot itself confirms movement.
- `IntentRejected(sequence, code, message)` is sent to the player group when validation fails later.
- Rate-limit and malformed-payload failures stay synchronous.

### Backpressure

- The channel remains bounded; a full queue rejects admission.
- The dispatcher drains a bounded number of intents per tick to protect the tick budget.
- Excess intents remain queued or are rejected with `QueueFull`; do not block the tick loop.

## Tasks

- [ ] Register `PlayerIntentDispatcher` and inject `ICommandQueue` into the engine.
- [ ] Add `DrainPlayerIntents` before systems.
- [ ] Add `Sequence` to `Intent` and per-player deduplication.
- [ ] Convert `MoveIntent` to a typed `MoveCommand`.
- [ ] Apply ownership checks through `CommandAuthorizationService`.
- [ ] Add async `IntentRejected` feedback.
- [ ] Bound per-tick intent processing.
- [ ] Add tests for drain order, duplicates, ownership, rejection and queue pressure.

## Files

- new `Genelife.API/Commands/PlayerIntentDispatcher.cs`
- `Genelife.API/Commands/Intent.cs`, `CommandQueue.cs`, `ICommandQueue.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Hubs/WorldHub.cs`
- `Genelife.API/Program.cs`
- `Genelife.UnitTests/Commands/IntentDrainTests.cs`

## Acceptance

- An enqueued intent is applied on the next tick.
- A duplicate sequence is applied once.
- An intent for a sim owned by another player is rejected.
- An intent for an unknown or dead sim is rejected.
- A full queue rejects admission without blocking the tick.
- Admin placement and player intents in the same tick see consistent map state.

## Testing

- Single-tick drain test.
- Duplicate/out-of-order sequence test.
- Ownership rejection.
- Queue-full test.
- Per-tick budget test.
- End-to-end hub → queue → tick test.

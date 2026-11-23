# P01 — Player agency and command contract

**Phase:** 1 — Foundation
**Depends:** V3 intent admission, V2 command queue, `PlayerControlled`
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Define the player action contract that every later V5 player feature uses. The contract must be typed, authorized, rate-limited, auditable and drained on the tick thread.

## Context

The browser currently submits `MoveIntent` and `UseBuildingIntent` through `WorldHub.SubmitIntent`. `SimulationEngine.DrainPlayerIntents()` applies them under the write lock. There is no contract for jobs, housing, shopping, family or continuation, and no common result or audit shape.

Adding ad-hoc endpoints per action would create several authorization paths and make replay and moderation harder. V5 needs one player command envelope with explicit action payloads.

## Design

### Action kinds

```csharp
public enum PlayerActionKind : byte
{
    ApplyForJob,
    AcceptJobOffer,
    QuitJob,
    RequestHousing,
    AcceptLease,
    MoveOut,
    PayArrears,
    BuyItem,
    ConsumeItem,
    InvitePartner,
    RespondToInvitation,
    TryForChild,
    NameChild,
    ChooseContinuation
}
```

### Command envelope

```csharp
public sealed record PlayerCommand
{
    public required string PlayerId { get; init; }
    public required long SimNetworkId { get; init; }
    public required PlayerActionKind Kind { get; init; }
    public required long Sequence { get; init; }
    public JsonElement Payload { get; init; }
    public long SubmittedTick { get; init; }
}
```

The envelope is transport-neutral. The hub and HTTP both admit it; the engine owns the typed payload parser.

### Result and audit

```csharp
public enum PlayerCommandStatus : byte
{
    Accepted,
    Rejected,
    Applied,
    Superseded
}

public sealed record PlayerCommandResult
{
    public Guid CommandId { get; init; }
    public string PlayerId { get; init; }
    public PlayerActionKind Kind { get; init; }
    public PlayerCommandStatus Status { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public long AppliedTick { get; init; }
}
```

Every command gets a command id, an admission result and, when applied, an audit row. Audit rows are written off the tick thread through the event writer.

### Rules

- Only the owner of the `PlayerControlled` sim may submit a command for it.
- The command sequence is monotonic per player; stale sequences are rejected.
- Per-player and per-world command budgets apply. A player cannot fill the queue.
- Commands that require proximity check the sim's tile against the building or sim target.
- Commands that require consent never apply immediately; they create an invitation with an expiry.
- A disconnected player cannot submit commands; queued commands are dropped on disconnect.
- Rejections use stable codes: `not_owner`, `sim_not_found`, `out_of_range`, `not_available`, `insufficient_funds`, `rate_limited`, `consent_required`, `invalid_state`.

### Drain order

1. Drain player commands.
2. Validate and stage effects.
3. Apply through domain services.
4. Emit events and results.
5. Run the normal systems.

No command may create, add, remove or destroy an entity inside a query callback.

## Tasks

- [ ] Add `PlayerActionKind`, `PlayerCommand`, `PlayerCommandStatus` and `PlayerCommandResult`.
- [ ] Add a typed payload parser per action with a descriptive failure.
- [ ] Route hub and HTTP admission through one `PlayerCommandService`.
- [ ] Add per-player sequence, rate limit and per-tick budget.
- [ ] Add command audit events and a rebuildable audit read model.
- [ ] Add tests for ownership, stale sequence, rate limit and malformed payload.
- [ ] Document the action codes in `Genelife.Contracts`.

## Files

- new `Genelife/Player/PlayerActionKind.cs`
- new `Genelife/Player/PlayerCommand.cs`
- new `Genelife/Player/PlayerCommandResult.cs`
- new `Genelife/Player/PlayerCommandService.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Commands/CommandQueue.cs`
- `Genelife.Contracts/Player/PlayerCommandDtos.cs`
- `Genelife.UnitTests/Player/PlayerCommandTests.cs`

## Acceptance

- A command from a non-owner is rejected with `not_owner`.
- A stale sequence is rejected and does not apply.
- A command over the per-player budget is rejected with `rate_limited`.
- A malformed payload is rejected without throwing.
- An accepted command applies once and only once.
- Every applied command has an audit row and a result visible to the player.
- The client never receives authority to mutate the world directly.

## Testing

- Submit valid and invalid commands for each action kind.
- Replay a recorded command log and compare the resulting player state.
- Assert the queue drains before systems and respects the per-tick budget.
- Assert a disconnect drops queued commands.
- Assert audit rows are idempotent on projection rebuild.

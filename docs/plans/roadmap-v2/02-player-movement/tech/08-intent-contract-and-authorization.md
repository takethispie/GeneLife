# P08 — Intent contract and authorization

**Phase:** 5 — Security
**Depends:** P02, P06
**Estimate:** 1–2 sessions
**Status:** Planned (V2)

## Goal

Define a stable, versioned player-intent contract and enforce ownership, bounds, reachability and rate limits on the server.

## Context

The current `MoveIntent` carries `float X` and `float Y` (`Genelife.API/Commands/Intent.cs`). `WorldHub.SubmitIntent` validates the payload shape, `PlayerId` claim and rate limit, then enqueues it. Ownership is not checked until the intent is applied, and there is no sequence number.

V1 has no player-facing client, so the contract can change without a long compatibility window.

## Design

### Contract

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MoveIntent), "move")]
[JsonDerivedType(typeof(UseBuildingIntent), "useBuilding")]
public abstract record Intent
{
    [JsonPropertyOrder(-1)]
    public string PlayerId { get; init; } = string.Empty;
    public long Sequence { get; init; }
}

public sealed record MoveIntent : Intent
{
    public int TargetTileX { get; init; }
    public int TargetTileY { get; init; }
    public ulong SimNetworkId { get; init; }
}

public sealed record UseBuildingIntent : Intent
{
    public ulong SimNetworkId { get; init; }
    public ulong BuildingNetworkId { get; init; }
}
```

Rules:

- `PlayerId` must match the authenticated claim.
- `Sequence` is monotonic per player/connection and is used for deduplication.
- `SimNetworkId` must exist, be alive and be controlled by the player.
- Coordinates are integer tiles. Floats are rejected at deserialization.
- Payload size and rate limits remain in `WorldHub`.

### Validation

| Check | Rejection |
|---|---|
| Missing/mismatched `PlayerId` | `PlayerMismatch` |
| Unknown sim | `UnknownSim` |
| Sim owned by another player | `NotOwned` |
| Dead sim | `SimDead` |
| Target outside bounds | `OutOfBounds` |
| Target blocked | `TargetBlocked` |
| No path | `NoPath` |
| Duplicate/old sequence | silently dropped or `StaleIntent` |
| Queue full | `QueueFull` |
| Payload too large | `PayloadTooLarge` |

### Results

Synchronous admission result stays small:

```json
{ "success": true, "sequence": 42, "acceptedAtTick": 1200 }
```

Asynchronous rejection is pushed to the player group:

```json
{ "type": "intentRejected", "sequence": 42, "code": "TargetBlocked", "message": "..." }
```

No error reveals another player's sim details.

### Anti-cheat boundaries

- A client cannot set `TilePosition`, speed or route directly.
- Movement distance is capped by the movement system per tick.
- A target is not trusted just because it is in range; the route must be walkable.
- Interaction checks range and role, not client assertions.
- Admin commands and player intents use separate authorization paths.

## Tasks

- [ ] Replace float `MoveIntent` coordinates with `TargetTileX/Y`.
- [ ] Add `Sequence` and `SimNetworkId`.
- [ ] Add `UseBuildingIntent`.
- [ ] Enforce ownership/role checks in the dispatcher.
- [ ] Add typed rejection codes and async feedback.
- [ ] Reject float/out-of-range payloads at deserialization.
- [ ] Update the multiplayer acceptance tests.
- [ ] Add a contract version note for client implementers.

## Files

- `Genelife.API/Commands/Intent.cs`
- `Genelife.API/Commands/PlayerIntentDispatcher.cs`
- `Genelife.API/Hubs/WorldHub.cs`
- `Genelife.API/Auth/CommandAuthorizationService.cs`
- `Genelife.UnitTests/Hubs/WorldHubTests.cs`
- `Genelife.UnitTests/Performance/MultiplayerAcceptanceTests.cs`

## Acceptance

- A valid move intent is admitted and applied on the next tick.
- A spoofed `PlayerId` or unowned sim is rejected.
- Out-of-bounds and blocked targets are rejected with specific codes.
- A duplicate sequence has no effect.
- Float coordinates are rejected by the contract.
- Existing hub rate limiting and payload limits still apply.
- The accepted/rejected contract is documented for clients.

## Testing

- Contract serialization tests.
- Ownership/spoofing tests.
- Bounds/blocked/no-path tests.
- Sequence deduplication.
- Rate-limit and payload-size tests.
- End-to-end hub → dispatcher → movement test.

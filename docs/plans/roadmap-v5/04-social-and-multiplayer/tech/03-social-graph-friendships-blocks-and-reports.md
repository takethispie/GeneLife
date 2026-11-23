# S03 — Social graph: friendships, blocks and reports

**Phase:** 3 — Graph
**Depends:** S01, feature 01 invitation patterns
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Persist the social edges that make presence, chat and player relationships safe: friendships, blocks, mutes and reports.

## Context

Presence and chat need a graph. Feature 01 has family invitations but no social edges. A block must be symmetric and must affect presence, chat and invitations, not just the UI.

## Design

### Edges

```csharp
public enum SocialEdgeKind : byte { Friend, Block, Mute }
public enum SocialEdgeSource : byte { Request, Admin, Migration }

public sealed record SocialEdge
{
    public required string FromPlayerId { get; init; }
    public required string ToPlayerId { get; init; }
    public SocialEdgeKind Kind { get; init; }
    public long SinceTick { get; init; }
    public SocialEdgeSource Source { get; init; }
}
```

Social edges are keyed by player id, not sim entity, so a player keeps friends and blocks after heir continuation. They live in the social graph service and are not ECS hot-state components. The simulation relationship edge is separate and is resolved from the current controlled sim when a partnership is accepted.

- `Friend` is mirrored: accepting creates both directions.
- `Block` is stored in both directions or enforced symmetrically by the service.
- `Mute` is one-directional: the muter hides the target.
- No self-edge.
- No duplicate `(FromPlayerId, ToPlayerId, Kind)` edge.

### Requests

```csharp
public enum SocialRequestKind : byte { Friend }
public enum SocialRequestStatus : byte { Pending, Accepted, Declined, Expired, Cancelled }

public sealed record SocialRequest
{
    public Guid Id { get; init; }
    public string FromPlayerId { get; init; }
    public string ToPlayerId { get; init; }
    public SocialRequestKind Kind { get; init; }
    public SocialRequestStatus Status { get; init; }
    public long CreatedTick { get; init; }
    public long ExpiresTick { get; init; }
}
```

- A friend request has an expiry and a cooldown after decline.
- Only the target can accept or decline.
- A block cancels pending requests in both directions.
- Accepting creates the mirrored friend edges and a notification.

### Reports

```csharp
public enum ReportReason : byte { Harassment, Spam, Hate, Impersonation, Other }
public enum ReportStatus : byte { Open, InReview, Resolved, Dismissed }

public sealed record SocialReport
{
    public Guid Id { get; init; }
    public string ReporterPlayerId { get; init; }
    public string TargetPlayerId { get; init; }
    public ReportReason Reason { get; init; }
    public Guid? MessageId { get; init; }
    public string? Evidence { get; init; }
    public long CreatedTick { get; init; }
    public ReportStatus Status { get; init; }
}
```

- A report is idempotent per reporter/target/message/reason window.
- Evidence is a snapshot of the reported message, not a live reference.
- Reports go to the moderation queue in step 06.

### Enforcement

- A block filters presence, chat, whispers and invitations in both directions.
- A mute filters chat only.
- A friendship enables whispers and the friend presence scope.
- Social edges are persisted in schema 15 and rebuilt on restore.

## Tasks

- [ ] Add `SocialEdge`, `SocialRequest` and `SocialReport`.
- [ ] Add `SocialGraphService` with create/remove/query.
- [ ] Enforce symmetric block behavior.
- [ ] Add friend request expiry and decline cooldown.
- [ ] Add report creation and evidence capture.
- [ ] Add read-model DTOs for friends, blocks and requests.
- [ ] Add admin visibility for edges and reports.
- [ ] Add tests for mirroring, duplicates, block symmetry and report idempotency.

## Files

- new `Genelife/Social/SocialEdge.cs`
- new `Genelife/Social/SocialRequest.cs`
- new `Genelife/Social/SocialReport.cs`
- new `Genelife/Social/SocialGraphService.cs`
- `Genelife.UnitTests/Social/SocialGraphTests.cs`

## Acceptance

- Accepting a friend request creates both edges.
- A duplicate edge is rejected.
- A block filters both directions for presence, chat and invitations.
- A mute filters chat only.
- A declined request starts a cooldown.
- A report captures evidence and is idempotent within the window.
- The graph survives restore and rebuild.

## Testing

- Request, accept and remove a friendship.
- Attempt a duplicate and a self-edge.
- Block and assert presence/chat/invite filtering.
- Mute and assert only chat filtering.
- Report twice and assert one item.
- Restore and compare the graph.

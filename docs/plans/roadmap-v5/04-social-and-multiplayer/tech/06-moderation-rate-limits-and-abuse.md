# S06 — Moderation, rate limits and abuse

**Phase:** 6 — Safety
**Depends:** S02, S03, S05
**Estimate:** 2–3 sessions
**Status:** Planned (V5)

## Goal

Make the social layer operable: bounded rates, a report queue, moderator actions, audit and abuse controls that work before launch.

## Context

Chat, presence, friend requests, invitations and venue activities all create abuse surfaces. A report button without a queue is worse than no report button.

Moderation must be able to act without reading the world snapshot or editing simulation state directly.

## Design

### Rate limits

| Action | Limit |
|---|---|
| Say / venue chat | configured messages per window |
| Whisper | lower per-window limit |
| Emote | cooldown per emote and per venue |
| Friend request | one pending per target and a decline cooldown |
| Partnership invitation | one pending per target and a cooldown |
| Report | deduplicated per target/message/reason window |
| Presence query | low-frequency polling only |

Limits are per player and per action, with a global per-world budget.

### Moderation queue

```csharp
public enum ModerationActionKind : byte { Warn, Mute, Block, RemoveMessage, CloseVenue, SuspendChat }

public sealed record ModerationAction
{
    public Guid Id { get; init; }
    public Guid? ReportId { get; init; }
    public string ModeratorId { get; init; }
    public string TargetPlayerId { get; init; }
    public ModerationActionKind Kind { get; init; }
    public long DurationTicks { get; init; }
    public string Reason { get; init; }
    public long CreatedTick { get; init; }
}
```

- A moderator can warn, mute, block, remove a message, close a venue or suspend chat.
- Every action is audited with the moderator, reason, duration and evidence.
- Actions are reversible where possible (unmute, unblock, reopen).
- A suspended player can still move but cannot chat or invite.

### Abuse controls

- A blocked player cannot target the blocker through presence, chat or invitations.
- A report captures evidence so later deletion does not erase it.
- Repeated reports from the same reporter are deduplicated.
- A player who reconnects keeps the same player id and the same blocks.
- A moderator can see report volume and repeat offenders.
- No automatic ban without a human decision in V5a.

### Retention

- Chat retention is configured; reports and moderation actions have a longer audit retention.
- Deleted messages are removed from player reads but remain as report evidence.
- Retention jobs run off the tick loop.

## Tasks

- [ ] Add rate-limit policies for each social action.
- [ ] Add the moderation queue and action records.
- [ ] Add moderator actions and reversal.
- [ ] Add evidence capture and retention.
- [ ] Add report deduplication and repeat-offender metrics.
- [ ] Add chat suspension and venue closure.
- [ ] Add admin endpoints and audit.
- [ ] Add abuse tests for flooding, report spam and block evasion.

## Files

- new `Genelife/Social/ModerationAction.cs`
- new `Genelife/Social/ModerationService.cs`
- new `Genelife/Social/SocialRateLimits.cs`
- new `Genelife.API/Social/ModerationEndpoints.cs`
- `Genelife.UnitTests/Social/ModerationTests.cs`

## Acceptance

- Every social action is rate-limited.
- A report creates one queue item within the dedupe window.
- A moderator can warn, mute, block, remove a message, close a venue and suspend chat.
- Every action is audited and reversible where applicable.
- A blocked player cannot evade the block by reconnecting.
- Evidence survives message deletion.
- Retention jobs do not run on the tick thread.

## Testing

- Flood each channel and assert the limit.
- Submit duplicate reports and assert one item.
- Apply and reverse each moderation action.
- Block, reconnect and assert the block holds.
- Delete a message and assert report evidence remains.
- Assert no moderation I/O on the tick thread.

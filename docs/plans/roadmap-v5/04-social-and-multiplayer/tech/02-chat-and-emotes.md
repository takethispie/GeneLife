# S02 — Chat and emotes

**Phase:** 2 — Communication
**Depends:** S01, feature 04 moderation hooks
**Estimate:** 2–3 sessions
**Status:** Planned (V5)

## Goal

Let players talk locally and send direct messages to friends, with rate limits, retention and a small set of world-readable emotes.

## Context

There is no chat today. V3 listed chat as a non-goal. A shared world with 32 players needs a communication path, but an unmoderated global chat would be a safety and operations problem from day one.

V5a is text only. Chat is not simulation state and must not enter the world snapshot or tick loop.

## Design

### Channels

```csharp
public enum ChatChannel : byte { Say, Whisper, Venue, System }
```

- `Say`: short radius or same venue.
- `Whisper`: direct message to a friend or an accepted conversation.
- `Venue`: everyone inside the same social venue.
- `System`: server notices; not player-authored.

### Message

```csharp
public sealed record ChatMessage
{
    public Guid Id { get; init; }
    public string PlayerId { get; init; }
    public string? DisplayName { get; init; }
    public ChatChannel Channel { get; init; }
    public string Text { get; init; }
    public long Tick { get; init; }
    public long? TargetPlayerId { get; init; }
    public long? VenueNetworkId { get; init; }
}
```

### Rules

- Maximum length is configured (default 240 characters).
- Rate limit per player per channel; excess is dropped with a notice.
- `Whisper` requires friendship or an explicit accepted conversation.
- A blocked pair cannot exchange messages in either direction.
- A muted player's messages are hidden from the muter, not deleted.
- Chat is written to Marten with a retention window; it is not persisted in the world snapshot.
- The client renders chat in a DOM panel, not in the Pixi canvas.
- Chat never allocates inside a simulation system.

### Emotes

```csharp
public enum EmoteKind : byte { Wave, Nod, ShakeHead, Sit, Stand, Cheer, Point }
```

- Emotes are predefined and rendered as a short world bubble or animation.
- Emotes have a cooldown and a per-venue budget.
- Emotes are public in the same radius as `Say`.
- Emotes are not free text and cannot carry a payload.

### Retention and moderation

- Default retention is 30 days, configurable.
- A report captures the message id, text snapshot, reporter, target, tick and reason.
- A moderator can remove a message and record a decision.
- Deleting a message removes it from future reads; the report keeps the evidence snapshot.

## Tasks

- [ ] Add `ChatChannel`, `ChatMessage` and `EmoteKind`.
- [ ] Add the chat admission service with length and rate limits.
- [ ] Add friend/block checks for whispers.
- [ ] Persist chat in Marten with retention.
- [ ] Add the client chat panel and emote bar.
- [ ] Add mute/block integration.
- [ ] Add report capture for a message.
- [ ] Add tests for rate limits, block, retention and message removal.

## Files

- new `Genelife/Social/ChatMessage.cs`
- new `Genelife/Social/ChatService.cs`
- new `Genelife/Social/EmoteService.cs`
- new `Genelife.API/Social/ChatStore.cs`
- `clients/browser/src/ui/social/chat.ts`
- `Genelife.UnitTests/Social/ChatTests.cs`

## Acceptance

- A local message reaches nearby or same-venue players only.
- A whisper requires friendship and is rejected otherwise.
- A blocked pair cannot exchange messages.
- Rate limits drop excess messages with a visible notice.
- Chat is retained for the configured window and removed after it.
- A report captures the message evidence.
- Emotes are predefined, cooldown-limited and public in the same radius.
- Chat does not enter the world snapshot or tick loop.

## Testing

- Send local, venue and whisper messages; assert recipients.
- Exceed the rate limit and assert the drop notice.
- Block a player and assert no delivery.
- Advance retention and assert removal.
- Report a message and assert the evidence snapshot.
- Assert the simulation has no chat allocation.

# S01 — Presence and player directory

**Phase:** 1 — Foundation
**Depends:** V3 hub lifecycle, feature 01 player identity, feature 02 appearance
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Expose a bounded, privacy-safe view of who is online and nearby, so players can find each other in the world without a global social network.

## Context

`WorldHub` knows each connection and its player sim. Snapshots already include other players' sims. There is no directory, no display name, no friend list and no privacy model.

The directory must not leak family, wallet, utility or trait data. It should be scoped by proximity, venue or friendship.

## Design

### Presence record

```csharp
public sealed record PlayerPresence
{
    public string PlayerId { get; init; }
    public string? DisplayName { get; init; }
    public long SimNetworkId { get; init; }
    public int TileX { get; init; }
    public int TileY { get; init; }
    public long? BuildingNetworkId { get; init; }
    public PresenceState State { get; init; }
    public AppearanceDescriptor Appearance { get; init; }
    public long LastSeenTick { get; init; }
}

public enum PresenceState : byte { Online, Afk, InVenue, Offline }
```

- `PlayerId` is the public opaque identity.
- `DisplayName` is optional, moderated and never a real name.
- `Appearance` is the compact descriptor from feature 02 so the directory can show a portrait; it contains no traits.
- Position is coarse (tile) and only exposed when the player is nearby or a friend.
- `InVenue` is set when the player is inside a social venue.

### Directory scopes

- `Nearby`: within a configured tile radius or the same venue.
- `Friends`: accepted friendship edges.
- `Venue`: players inside the same venue.
- No global search, no full player list and no offline history.

### Presence lifecycle

- Connect creates a presence record and a hub mapping.
- Disconnect marks `Offline` after a grace period and removes the record from public scopes.
- Reconnect restores the record from the player profile.
- A stale presence older than the grace period is not shown.
- A blocked player is filtered from both directions.

### Privacy

- The directory never includes wallet, family, traits, utility, job or health.
- A player can hide their display name and appear as a generic resident.
- A player can disable nearby visibility for non-friends.
- Admin sees the full presence list; players see only allowed scopes.

## Tasks

- [ ] Add `PlayerPresence`, `PresenceState` and the public DTO.
- [ ] Add `PresenceRegistry` with nearby/venue/friend scopes.
- [ ] Wire connect, disconnect, reconnect and stale expiry.
- [ ] Add block filtering hooks from feature 04 step 03.
- [ ] Add privacy settings and display-name moderation.
- [ ] Add `GET /api/social/presence` player-scoped endpoints.
- [ ] Add the client player-directory panel.
- [ ] Add tests for scopes, privacy, stale expiry and reconnect.

## Files

- new `Genelife/Social/PlayerPresence.cs`
- new `Genelife/Social/PresenceRegistry.cs`
- new `Genelife.Contracts/Social/PlayerPresenceDto.cs`
- `Genelife.API/Hubs/WorldHub.cs`
- `clients/browser/src/ui/social/*`
- `Genelife.UnitTests/Social/PresenceTests.cs`

## Acceptance

- A player sees only nearby, venue or friend presence.
- A blocked player is absent from the directory.
- A disconnected player disappears after the grace period.
- Reconnect restores the presence record.
- The directory never exposes private state.
- The client panel updates at a low frequency without affecting the render loop.

## Testing

- Connect two players nearby and assert mutual visibility.
- Move one away and assert the nearby scope drops it.
- Block one and assert both directions are filtered.
- Disconnect and assert stale expiry.
- Assert private fields are absent from the DTO.
- Assert a hidden display name renders the fallback.

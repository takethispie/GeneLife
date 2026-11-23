# S05 — Shared venues and activities

**Phase:** 5 — World
**Depends:** S01, S02, V2 buildings and placement
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Give players a reason to be in the same place: a small set of existing world venues where presence, local chat and simple activities are visible.

## Context

The map already has residential, office, school and civic buildings. There is no concept of a social venue, no venue presence and no activity beyond building use.

V5a does not add a separate lobby. It marks a few existing buildings as social venues and uses proximity for presence and chat.

## Design

### Venue

```csharp
public enum SocialVenueKind : byte { Cafe, Park, Civic, OfficeLobby, SchoolYard }

[Flags]
public enum VenueActivityMask : byte
{
    None = 0,
    Sit = 1 << 0,
    Talk = 1 << 1,
    ShareMeal = 1 << 2,
    PlayGame = 1 << 3,
    Cheer = 1 << 4
}

public record struct SocialVenue
{
    public SocialVenueKind Kind;
    public int Capacity;
    public long OpenFromTick;
    public long OpenToTick;
    public VenueActivityMask Activities;
}
```

- A venue is a building with a `SocialVenue` component.
- Capacity limits concurrent occupants.
- Opening hours use the existing clock.
- Activities are a bitmask of allowed actions.

### Presence

- A player inside the venue footprint or entry tile is `InVenue`.
- The venue directory lists occupants.
- A venue channel is available to occupants.
- Leaving the venue removes the venue presence and channel access.

### Activities

```csharp
public enum VenueActivity : byte { Sit, Talk, ShareMeal, PlayGame, Cheer }
```

- Activities are proximity actions with a short cooldown.
- `ShareMeal` consumes a pantry item if the player has one; it does not mint food.
- `PlayGame` emits a social event but does not create money.
- Activities are visible to other occupants as a short emote or status.
- Activities never bypass needs, wallet or household rules.

### Venue capacity and safety

- Capacity is enforced at entry; a full venue rejects entry with a visible reason.
- A blocked pair can share a venue but cannot chat or invite.
- A moderator can close a venue or remove a player from it.
- Venue presence is public within the venue only.

## Tasks

- [ ] Add `SocialVenue`, `SocialVenueKind`, `VenueActivity` and the activity mask.
- [ ] Mark the configured buildings as venues.
- [ ] Add venue presence and the venue directory.
- [ ] Add the venue chat channel.
- [ ] Add activity admission, cooldown and events.
- [ ] Add capacity and opening-hours enforcement.
- [ ] Add the client venue panel and activity bar.
- [ ] Add tests for presence, capacity, activities and block behavior.

## Files

- new `Genelife/Components/Social/SocialVenue.cs`
- new `Genelife/Social/VenueService.cs`
- new `Genelife/Social/VenueActivityService.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `clients/browser/src/ui/social/venue.ts`
- `Genelife.UnitTests/Social/VenueTests.cs`

## Acceptance

- A player entering a venue becomes visible to other occupants.
- The venue channel reaches only occupants.
- A full venue rejects entry.
- A closed venue rejects entry.
- Activities respect cooldown and allowed mask.
- `ShareMeal` consumes a real pantry item.
- A blocked pair cannot chat or invite inside the venue.
- Leaving removes venue presence and channel access.

## Testing

- Enter and leave a venue; assert presence.
- Fill capacity and assert rejection.
- Send a venue message and assert recipients.
- Run each activity and assert cooldown and event.
- Share a meal and assert pantry consumption.
- Block a pair and assert no chat.

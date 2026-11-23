# S07 — Persistence, privacy and admin

**Phase:** 7 — Durability
**Depends:** S01–S06, schema 14
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Persist the social graph and privacy settings, keep chat retention out of the world snapshot, and give operators a Social admin page.

## Context

Schema 14 adds consumption. Schema 15 adds social edges, requests, reports and privacy settings. Chat is a Marten document with retention, not world state.

The admin needs a Social page for presence, reports, blocks, friendships, player partnerships and moderation actions.

## Design

### Schema 15

`PersistedWorldState.CurrentSchemaVersion` moves from 14 to 15.

New persisted blocks:

```csharp
public sealed class PersistedSocialEdge
{
    public string FromPlayerId { get; set; } = "";
    public string ToPlayerId { get; set; } = "";
    public SocialEdgeKind Kind { get; set; }
    public long SinceTick { get; set; }
    public SocialEdgeSource Source { get; set; }
}

public sealed class PersistedSocialRequest
{
    public Guid Id { get; set; }
    public string FromPlayerId { get; set; } = "";
    public string ToPlayerId { get; set; } = "";
    public SocialRequestKind Kind { get; set; }
    public SocialRequestStatus Status { get; set; }
    public long ExpiresTick { get; set; }
}

public sealed class PersistedSocialSettings
{
    public string PlayerId { get; set; } = "";
    public string? DisplayName { get; set; }
    public bool NearbyVisible { get; set; }
    public bool FriendsOnlyMessages { get; set; }
}
```

Reports and moderation actions are Marten documents with audit retention. Chat is a Marten document with its own retention.

### Migration

- Schema 14 worlds get empty social edges, requests and reports.
- Player profiles get default privacy settings.
- No friendships, blocks or partnerships are invented.
- Chat starts empty.
- A second restore produces identical social state.

### Admin page

`/social` supports:

- online presence and venue occupancy;
- report queue with evidence and status;
- blocks, mutes and friendships;
- player partnerships and cohabitation invitations;
- moderation history and reversal;
- chat retention and deletion status;
- privacy settings and display-name moderation.

### Privacy

- Player routes expose only public presence and the player's own settings.
- Admin routes require the admin policy.
- No real names, emails, IP addresses or external ids are persisted.
- Chat content is not part of the world snapshot.

## Tasks

- [ ] Add the schema 15 persisted blocks and migration.
- [ ] Add default privacy settings for player profiles.
- [ ] Add social read-model DTOs and endpoints.
- [ ] Add the `/social` admin page.
- [ ] Add Marten documents for reports, moderation and chat.
- [ ] Add retention jobs and deletion status.
- [ ] Add migration, restore and double-restore tests.
- [ ] Add HTTP smoke tests for unauthorized access.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Persistence/SnapshotMigrations.cs`
- new `Genelife.API/Social/SocialReadModel.cs`
- new `Genelife.API/Endpoints/SocialEndpoints.cs`
- new `Genelife.Web/Pages/Social.razor`
- `Genelife.UnitTests/Persistence/SocialPersistenceTests.cs`

## Acceptance

- Schema 14 saves migrate to 15 and restore exactly.
- A second restore does not change social state.
- Chat retention does not make the world snapshot unrestorable.
- The admin page shows presence, reports, edges and moderation.
- Player routes never expose another player's private state.
- All social admin routes require the admin policy.
- Events and documents are idempotent and rebuildable.

## Testing

- Migrate a schema 14 save and compare social state.
- Double restore and compare.
- Save/restore friends, blocks, requests and settings.
- Assert chat is absent from the snapshot.
- Assert unauthorized `/api/social` admin routes return 401/403.
- Assert retention removes old chat but keeps report evidence.

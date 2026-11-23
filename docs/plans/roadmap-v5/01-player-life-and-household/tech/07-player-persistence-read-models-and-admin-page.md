# P07 — Player persistence, read models and admin page

**Phase:** 7 — Durability
**Depends:** P01–P06, schema 11 persistence
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Persist the player life state, expose a player read model and give operators a safe Player Life admin page.

## Context

Schema 11 already persists `PlayerControlled`, `PlayerId`, `HasWallet` and the V4 family state. It does not persist player employment, leases, pantries, invitations, continuation preference or action audit.

The admin has Data, Economy, Housing, Lifecycle, Map and Family pages. None of them shows the player's life as a coherent whole.

## Design

### Schema 12

`PersistedWorldState.CurrentSchemaVersion` moves from 11 to 12.

New persisted blocks:

```csharp
public sealed class PersistedPlayerProfile
{
    public string PlayerId { get; set; } = "";
    public long ControlledNetworkId { get; set; }
    public PlayerContinuationPolicy ContinuationPolicy { get; set; }
    public bool AwaitingContinuation { get; set; }
}

public sealed class PersistedPlayerLife
{
    public long NetworkId { get; set; }
    public long? OfficeNetworkId { get; set; }
    public long? HouseholdNetworkId { get; set; }
    public long? UnitNetworkId { get; set; }
    public long LeaseStartTick { get; set; }
    public int FoodUnits { get; set; }
    public int HouseholdGoods { get; set; }
}
```

The profile is keyed by `PlayerId`; heir continuation updates `ControlledNetworkId` instead of creating a second player identity. `PersistedPlayerLife` is keyed by the controlled sim's network id because a job, lease and pantry belong to that life, not to the player identity.

Invitations are persisted as `PersistedFamilyInvitation`. Command audit is a Marten document with retention, not world-snapshot state.

### Migration

- Schema 11 player sims get a profile with the default continuation policy.
- Employment and housing are derived from the existing `Employee` and household state where possible.
- Pantry starts empty.
- No player family edges or invitations are invented for legacy saves.
- A second restore must produce identical player state.

### Read model

`PlayerLifeStatus` includes:

- player id and sim network id;
- alive/dead and continuation state;
- job, employer, wage and attendance;
- household, unit, lease, rent and arrears;
- wallet, savings and pantry;
- partner, children, guardianship and invitations;
- last actions and their results;
- health, needs and age.

The admin page is `/player`, with filters for player id, employment, housing, family state and continuation state.

### Events and projections

- `PlayerProfileCreated`, `PlayerHired`, `PlayerQuitJob`, `PlayerLeaseStarted`, `PlayerMoved`, `PlayerBoughtItem`, `PlayerInvited`, `PlayerContinuationApplied`.
- Projections are idempotent and rebuildable.
- The admin page can show a stale-projection warning and fall back to the live read model.

## Tasks

- [ ] Add the schema 12 persisted blocks and migration.
- [ ] Add `PlayerLifeStatus` DTOs and the live read model.
- [ ] Add the `/api/player` endpoints with admin authorization.
- [ ] Add the `/player` admin page with tabs and filters.
- [ ] Add player events and Marten projections.
- [ ] Add action audit retention and query.
- [ ] Add migration, restore and double-restore tests.
- [ ] Add HTTP smoke tests for unauthorized and authorized access.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Persistence/SnapshotMigrations.cs`
- new `Genelife.API/Player/PlayerReadModel.cs`
- new `Genelife.API/Endpoints/PlayerEndpoints.cs`
- new `Genelife.Web/Pages/Player.razor`
- new `Genelife.Messages/Events/Player*.cs`
- `Genelife.UnitTests/Persistence/PlayerPersistenceTests.cs`

## Acceptance

- Schema 11 saves migrate to 12 and restore exactly.
- A second restore does not change player state.
- A player's job, lease, pantry, family and continuation state survive save/restore.
- Chat or command audit does not make the world snapshot unrestorable.
- The `/player` page shows the player's life and supports empty, seeded and migrated worlds.
- All `/api/player` admin routes require the admin policy; player-facing routes never expose another player's private state.
- Events have idempotent projections and a rebuild path.

## Testing

- Save/restore a player with a job, home, pantry, partner and child.
- Double restore and compare.
- Migrate a schema 11 save with a player sim.
- Rebuild projections and compare documents.
- Assert unauthorized `/api/player` returns 401/403.
- Assert the page renders with no players and with multiple players.

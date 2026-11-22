# Step 03 — Stable network identifiers

**Depends:** none
**Goal:** Give every client-visible entity a stable ID that survives Arch entity-ID recycling.

**Status:** Done

## Context

Arch recycles entity IDs and tracks a `Version`; exposing `entity.Id` to clients is unsafe
in a long-lived world. `CompanyStatus.EntityId` currently leaks it.

## Tasks

- [x] Add `NetworkId { ulong Value }` component.
- [x] Add a tick-thread-owned allocator (monotonic counter or GUID).
- [x] Maintain `Dictionary<NetworkId, Entity>` and a reverse map.
- [x] Assign `NetworkId` when creating Sims, companies, offices, and world objects.
- [x] Remove `NetworkId` from maps when the entity is destroyed (emit a destroy/tombstone event).
- [x] Replace `EntityId` in all client DTOs with `NetworkId`.
- [x] Add O(1) lookup helper used by repositories and commands.

## Files likely touched

- new `Genelife/Components/Gen/NetworkId.cs`
- new `Genelife.Api/Services/NetworkIdRegistry.cs`
- `Genelife.Api/Repositories/*`, `Genelife.Api/DTOs/*`

## Acceptance

- No raw Arch `Entity.Id` appears in any client-facing payload.
- Destroy then create never reuses a `NetworkId`.
- Lookups are O(1).

## Testing

- Create/destroy/recreate cycles; assert no collisions and map cleanup.

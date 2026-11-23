# R08 — Relationship events and projections

**Phase:** 8 — Events
**Depends:** R01–R07
**Estimate:** 1–2 sessions
**Status:** Planned (V4)

## Goal

Publish relationship facts and build idempotent read-model projections without blocking the tick loop.

## Context

`SimulationEngine` enqueues events to `SimulationEventWriter`, which persists them asynchronously through Marten. Existing projections cover sims, companies and maps. Relationship state needs the same pattern.

## Design

### Events

Add to `Genelife.Messages/Events/FamilyEvents.cs`:

```csharp
public sealed record RelationshipFormed(
    ulong RelationshipNetworkId,
    ulong FromNetworkId,
    ulong ToNetworkId,
    string Kind,
    string Source,
    long Tick);

public sealed record PartnershipFormed(
    ulong RelationshipNetworkIdA,
    ulong RelationshipNetworkIdB,
    ulong SimNetworkIdA,
    ulong SimNetworkIdB,
    int AttractionScore,
    long Tick);

public sealed record PartnershipEnded(
    ulong SimNetworkIdA,
    ulong SimNetworkIdB,
    string Reason,
    long Tick);

public sealed record PairingRejected(
    ulong SimNetworkIdA,
    ulong SimNetworkIdB,
    int Score,
    string Reason,
    long Tick);

public sealed record HouseholdMerged(
    ulong HouseholdNetworkId,
    ulong FromHouseholdNetworkId,
    ulong SimNetworkId,
    long Tick);

public sealed record HouseholdSplit(
    ulong HouseholdNetworkId,
    ulong NewHouseholdNetworkId,
    ulong SimNetworkId,
    long Tick);
```

Names are stable; changing a payload is a contract change.

### Projections

- `FamilyProjection` — one document per sim with parents, children, partner, guardian and household ids.
- `RelationshipProjection` — one document per edge, keyed by relationship network id.
- `HouseholdCompositionProjection` — member count, dependants and overcrowding.
- Projections are idempotent and rebuildable from the event stream.

### Tick-thread boundary

- The tick creates events only; it never awaits Marten.
- The event writer batches and persists off-thread.
- A projection failure is logged and retried; it must not stop the simulation.
- The admin read model can fall back to the live engine when projections are stale.

## Tasks

- [ ] Add the event records and stable field names.
- [ ] Emit events from the relationship, pairing and household services.
- [ ] Add the three projections.
- [ ] Register projections with Marten.
- [ ] Add projection rebuild tests.
- [ ] Add a stale-projection indicator to the admin family status.

## Files

- new `Genelife.Messages/Events/FamilyEvents.cs`
- new `Genelife.API/Projections/FamilyProjection.cs`
- new `Genelife.API/Projections/RelationshipProjection.cs`
- `Genelife.API/Services/SimulationEventWriter.cs`
- `Genelife.UnitTests/Snapshots/FamilyProjectionTests.cs`

## Acceptance

- Every edge/pair/merge event is emitted exactly once per operation.
- Projections are idempotent when the same event is applied twice.
- A rebuild from the event stream produces the same documents as the incremental path.
- A projection failure does not stop the tick loop.
- The admin status exposes projection staleness.

## Testing

- Apply each event twice and compare the projection.
- Rebuild from a fixture event stream.
- Simulate a projection exception and assert the engine continues.
- Assert event field names and ids are stable.
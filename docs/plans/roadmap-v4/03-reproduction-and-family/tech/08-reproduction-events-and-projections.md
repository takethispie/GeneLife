# F08 — Reproduction events and projections

**Phase:** 8 — Events
**Depends:** F01–F07
**Estimate:** 1–2 sessions
**Status:** Partial — events emitted; Marten projections pending

## Goal

Publish pregnancy, birth, childhood and guardian facts to the read model without blocking the tick loop.

## Context

`SimulationEventWriter` persists events asynchronously. Relationship events are defined in R08; reproduction adds a separate event family and projections.

## Design

### Events

```csharp
public sealed record PregnancyStarted(
    ulong PregnancyNetworkId,
    ulong CarryingParentNetworkId,
    ulong OtherParentNetworkId,
    long ConceivedTick,
    long DueTick,
    long Tick);

public sealed record PregnancyProgressed(
    ulong PregnancyNetworkId,
    int RiskBasisPoints,
    int MonthsRemaining,
    long Tick);

public sealed record PregnancyComplication(
    ulong PregnancyNetworkId,
    string Kind,
    int RiskBasisPoints,
    long Tick);

public sealed record PregnancyLost(
    ulong PregnancyNetworkId,
    string Cause,
    long Tick);

public sealed record ChildBorn(
    ulong ChildNetworkId,
    ulong ParentANetworkId,
    ulong ParentBNetworkId,
    ulong HouseholdNetworkId,
    long Tick);

public sealed record BirthRecorded(
    ulong ChildNetworkId,
    ulong PregnancyNetworkId,
    string Name,
    long Tick);

public sealed record GuardianshipAssigned(
    ulong ChildNetworkId,
    ulong GuardianNetworkId,
    string Source,
    long Tick);

public sealed record ChildCostPaid(
    ulong ChildNetworkId,
    ulong HouseholdNetworkId,
    string Category,
    long AmountCents,
    long Tick);

public sealed record ChildBenefitPaid(
    ulong HouseholdNetworkId,
    int Children,
    long AmountCents,
    long Tick);
```

### Projections

- `PregnancyProjection` — one document per pregnancy with status, parents, due tick and risk.
- `ChildProjection` — one document per child with parents, guardian, household, school and age.
- Extend `HouseholdCompositionProjection` with children and dependants.
- A family timeline projection can be derived from the event stream later; do not persist a second copy in V4a.

### Tick-thread boundary

- The systems emit events only.
- Projections are idempotent and rebuildable.
- A projection failure is logged and retried; it does not stop the simulation.
- The admin can fall back to the live engine if projections are stale.

## Tasks

- [ ] Add the event records with stable field names.
- [ ] Emit from conception, pregnancy, birth, guardian and child-cost paths.
- [ ] Add the pregnancy and child projections.
- [ ] Extend household composition.
- [ ] Register the projections.
- [ ] Add rebuild and idempotency tests.
- [ ] Add projection staleness to the family status.

## Files

- `Genelife.Messages/Events/FamilyEvents.cs`
- new `Genelife.API/Projections/PregnancyProjection.cs`
- new `Genelife.API/Projections/ChildProjection.cs`
- `Genelife.API/Services/SimulationEventWriter.cs`
- `Genelife.UnitTests/Snapshots/ReproductionProjectionTests.cs`

## Acceptance

- Every pregnancy/birth/child-cost operation emits the expected event once.
- Applying an event twice does not duplicate a projection.
- A rebuild produces the same documents as the incremental path.
- A projection exception does not stop the tick loop.
- The admin family status reports stale projections.

## Testing

- Apply each event twice.
- Rebuild from a fixture stream.
- Simulate a projection failure.
- Assert child and pregnancy documents match the live engine for a seeded family.
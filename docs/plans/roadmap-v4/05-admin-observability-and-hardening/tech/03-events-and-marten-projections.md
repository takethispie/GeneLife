# H03 — Events and Marten projections

**Phase:** 3 — Events
**Depends:** R08, F08, C02–C04
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Unify the family event family and projections so the admin history is complete, idempotent and rebuildable.

## Context

`SimulationEventWriter` persists queued events asynchronously. Existing projections cover sims, companies and maps. V4 adds relationships, pregnancies, births, guardianship, trusts and inheritance.

## Design

### Event families

| Family | Examples |
|---|---|
| Relationships | `RelationshipFormed`, `PartnershipFormed`, `PartnershipEnded`, `PairingRejected` |
| Reproduction | `PregnancyStarted`, `PregnancyProgressed`, `PregnancyLost`, `ChildBorn` |
| Guardianship | `GuardianshipAssigned`, `StateCareOpened`, `StateCareClosed` |
| Inheritance | `EstateSettled`, `InheritancePaid`, `TrustDeposited`, `TrustReleased`, `EscheatRecorded` |
| Household | `HouseholdMerged`, `HouseholdSplit` |
| Admin | `FamilyAdminActionApplied` |

Every event carries the tick, stable network ids, source and, for admin actions, the reason.

### Projections

| Projection | Document | Key |
|---|---|---|
| `FamilyProjection` | family summary per sim | sim network id |
| `RelationshipProjection` | edge document | relationship network id |
| `PregnancyProjection` | pregnancy document | pregnancy network id |
| `ChildProjection` | child document | child network id |
| `HouseholdCompositionProjection` | household composition | household network id |
| `EstateProjection` | estate/inheritance history | deceased network id |

Projections are idempotent and rebuildable from the event stream.

### Consistency

- The live engine is authoritative for the current tick.
- Projections may lag; the admin shows a staleness warning.
- A projection failure is logged and retried; it never stops the tick loop.
- A rebuild is a supported operation, not a manual database edit.

### Event volume

- Pairing rejections can be high-volume; sample or aggregate them if the event store grows too fast.
- Pregnancy progression is monthly per active pregnancy; acceptable.
- Child costs are monthly per child; aggregate per household if needed.
- Document the volume decision in the projection.

## Tasks

- [ ] Finalize the event records and stable names.
- [ ] Add the projections and Marten registrations.
- [ ] Add idempotency and rebuild tests.
- [ ] Add the staleness indicator.
- [ ] Add an event-volume guard for rejections.
- [ ] Add a projection failure test.

## Files

- `Genelife.Messages/Events/FamilyEvents.cs`
- `Genelife.API/Projections/FamilyProjection.cs`
- `Genelife.API/Projections/RelationshipProjection.cs`
- `Genelife.API/Projections/PregnancyProjection.cs`
- `Genelife.API/Projections/ChildProjection.cs`
- `Genelife.API/Services/SimulationEventWriter.cs`
- `Genelife.UnitTests/Snapshots/FamilyProjectionTests.cs`

## Acceptance

- Every family operation emits at least one event.
- Applying an event twice does not duplicate a document.
- A rebuild produces the same documents as the incremental path.
- A projection exception does not stop the simulation.
- The admin shows projection staleness.
- Event volume stays within the configured budget.

## Testing

- Idempotency for each projection.
- Rebuild from a fixture stream.
- Failure injection.
- Volume accounting for a decade.
- Staleness indicator test.
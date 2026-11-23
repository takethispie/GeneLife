# H01 — Family read model and DTOs

**Phase:** 1 — Admin
**Depends:** features 01–04
**Estimate:** 1–2 sessions
**Status:** Planned (V4)

## Goal

Expose the family system through stable, paginated read DTOs so the admin page and future clients do not reach into the world.

## Context

`Genelife.Contracts` already holds polling DTOs such as `LifecycleStatus`, `SimLifecycleStatus` and `PopulationSnapshotStatus`. The family read model follows the same pattern.

## Design

### DTO groups

| DTO | Contents |
|---|---|
| `FamilyStatus` | living sims, eligible singles, partnered, parents, children, trait distribution, continuity summary, health |
| `TraitDistributionStatus` | counts/shares per trait, sex ratio, mutation rate, migrated count |
| `RelationshipStatus` | edge id, from/to, kind, source, since tick |
| `AttractionExplanation` | score, eligibility, rejection reason, component list |
| `PregnancyStatus` | parents, due tick, months remaining, risk, status, care paid |
| `ChildStatus` | parents, guardian, household, school, overcrowding, child costs |
| `GuardianshipStatus` | guardian/ward, source, since tick, state-care flag |
| `FamilyHealthCheckStatus` | name, level, detail |
| `HouseholdCompositionStatus` | members, children, dependants, capacity, overcrowding |

### Engine builders

Add to `SimulationEngine`:

- `BuildFamilyStatus()`
- `BuildFamilySims(take, skip, stage, partnered, hasChildren)`
- `BuildFamilySim(simId)`
- `BuildRelationships(simId?, kind?, source?, take, skip)`
- `BuildPregnancies(activeOnly, take, skip)`
- `BuildChildren(ageBand?, guardianId?, take, skip)`
- `BuildAttractionExplanation(a, b)`
- `BuildFamilyHealth()`

Builders use the live world and index; they do not mutate.

### Rules

- Traits are admin-only in V4a.
- No DTO exposes a global attractiveness ranking.
- Lists are paginated and filtered on the server.
- DTO field names are stable; changing a field is a contract change.
- A stale projection indicator is included where the admin may be reading projected data.

## Tasks

- [ ] Add the DTO group to `Genelife.Contracts`.
- [ ] Add the engine builders with paging/filtering.
- [ ] Add the family endpoints.
- [ ] Add DTO contract tests.
- [ ] Add a stale-projection indicator.
- [ ] Document admin-only trait visibility.

## Files

- `Genelife.Contracts/Dtos/FamilyStatus.cs`
- `Genelife.API/Endpoints/Wolverine/FamilyEndpoints.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Endpoints/FamilyEndpointTests.cs`

## Acceptance

- Every DTO serializes with stable names.
- Lists respect paging and filters.
- A sim detail includes parents, children, partner and guardian.
- Attraction explanation includes components and eligibility.
- No DTO exposes trait ranking or a player partner search.
- Health checks are included.

## Testing

- Endpoint shape tests.
- Paging and filter tests.
- Empty, seeded and migrated worlds.
- Authorization: admin sees traits, player does not.
- Snapshot of a representative DTO for contract review.
# Step 01 — Query-safe structural changes

**Depends:** none
**Goal:** Remove all component add/remove/destroy calls from inside World query callbacks.

**Status:** Done

## Context

Arch requires no structural changes during query iteration. `HiringSystem` already uses
collect → decide → apply; two other sites violate it.

## Tasks

- [x] Refactor `JobSeekerSystem.Update`: collect eligible entities, then `world.Add` after the query.
- [x] Refactor `CompanyRepository.Delete`: collect the target entity, destroy it after the query.
- [x] Grep for `world.Add|world.Remove|world.Destroy` inside `world.Query` lambdas; fix any others.
- [x] Add a comment convention: structural changes only in an `Apply` phase.
- [x] Add regression tests for both paths.

## Files likely touched

- `Genelife/Systems/JobSeekerSystem.cs`
- `Genelife.Api/Repositories/CompanyRepository.cs`
- `Genelife.UnitTests/Systems/JobSeekerSystemTests.cs`

## Acceptance

- No structural mutation occurs inside any query callback.
- Eligible Sims become job seekers exactly once.
- Deleting a company succeeds and leaves archetypes intact.

## Testing

- Entity count and component presence before/after each operation.
- Run the full system test suite.

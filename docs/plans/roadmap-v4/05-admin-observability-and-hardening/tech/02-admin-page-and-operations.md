# H02 — Admin page and operations

**Phase:** 2 — Admin
**Depends:** H01, R09, F09
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Ship one coherent family admin page with inspection and controlled operations, using the existing Blazor, polling, command and authorization patterns.

## Context

`Genelife.Web` already has Data, Economy, Housing, Lifecycle and Map pages. Relationship and reproduction tech steps add endpoints and partial UI; this step makes the page complete and consistent.

## Design

### Page structure

`Genelife.Web/Pages/Family.razor`:

| Tab | Contents |
|---|---|
| Overview | counts, partnership rate, births, trait distribution, continuity health |
| Sims | filter by stage, age, household, partnered, children, orphan; open a sim |
| Relationships | parents, children, partner, guardian, source and history |
| Attraction | choose two sims, show score components and eligibility reason |
| Pregnancies | due date, risk, parents, care, status; force/cancel |
| Children | guardian, household, school, overcrowding, costs |
| Health | invariant list, projection staleness, failing entity links |

### Operations

| Operation | Command | Notes |
|---|---|---|
| Force partnership | `ForcePartnershipCommand` | admin, audited, eligibility override explicit |
| End partnership | `EndPartnershipCommand` | reason required |
| Force pregnancy | `StartPregnancyCommand` | admin/testing, one-active rule |
| Cancel pregnancy | `CancelPregnancyCommand` | reason required |
| Assign guardian | `AssignGuardianCommand` | admin, replaces state care |
| Release trust | `ReleaseTrustCommand` | admin override only, audited |

All operations return `202` and apply on the next tick.

### Page rules

- Show the source of every relationship/pregnancy/guardianship (`Seed`, `Attraction`, `Admin`, `Death`, `Migration`).
- Never default-sort by a trait.
- Never show a global attractiveness leaderboard.
- Show empty states and command results.
- Link failing health rows to the affected sim/household.
- Keep the page admin-only in V4a; no player navigation entry.

### Security

- Read endpoints require the admin policy.
- Mutations require admin and an idempotency key.
- The page never writes the world directly.
- A destructive operation asks for confirmation and records the reason.

## Tasks

- [ ] Add the Family page and navigation.
- [ ] Add the tabs, filters and empty states.
- [ ] Add the operation forms and command results.
- [ ] Add health-row links.
- [ ] Add admin-only authorization.
- [ ] Add page smoke and authorization tests.
- [ ] Document the operation reasons.

## Files

- `Genelife.Web/Pages/Family.razor`
- `Genelife.Web/Layout/NavMenu.razor`
- `Genelife.API/Commands/AdminCommand.cs`
- `Genelife.API/Handlers/FamilyCommandHandlers.cs`
- `Genelife.UnitTests/Endpoints/FamilyEndpointTests.cs`

## Acceptance

- The page loads for a seeded family and an empty world.
- Every tab uses real data and an empty state.
- Operations return `202` and are visible after the next tick.
- Non-admin access is rejected.
- A destructive operation is audited with a reason.
- No player route exposes the page.

## Testing

- Playwright or bUnit smoke test for the page shell.
- Endpoint authorization tests.
- Command apply-and-observe test.
- Empty/seed/migrated fixture tests.
- Assert no trait-sorted default and no attractiveness leaderboard.
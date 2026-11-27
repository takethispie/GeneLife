# Operator Parity Coverage

## Context

`DocsConsistencyTests.Moved_routes_are_declared_on_wolverine_endpoints` already asserts that moved routes are declared on Wolverine endpoints, and the endpoint parity tests cover the routes migrated under V6. This step extends that coverage to whatever this track moves, so the migration is checked by the suite rather than by reading the diff.

Coverage matters here more than in a normal refactor because the change is invisible when it regresses: an endpoint that quietly goes back to waiting still returns 200 with the right body, just slower and coupled to the tick.

## Design

1. **Parity per migrated route.** For each route in the inventory, assert the declaration and the response shape, following the existing `Moved_routes_are_declared_on_wolverine_endpoints` pattern rather than inventing a parallel mechanism.
2. **Assert the property, not the implementation.** The property that matters is "does not wait for the next tick". Where that cannot be asserted directly, assert the observable consequence: a `202` with a command id, and the command applying on a subsequent tick.
3. **Smoke the operator path end to end.** The admin smoke test should exercise one migrated endpoint through the command id, so the accepted -> applied transition is covered and not just the response code.
4. **State what is not covered.** If any endpoint's blocking behaviour cannot be asserted without a timing-sensitive test, say so explicitly rather than implying coverage — the same rule already applied to SignalR group membership.

## Tasks

- [ ] Extend the route-declaration assertions to every route moved by this track.
- [ ] Assert `202 Accepted` + command id for each migrated endpoint.
- [ ] Assert the command is applied on a later tick, via the command id.
- [ ] Extend the admin smoke test to cover one migrated endpoint end to end.
- [ ] Record any endpoint whose blocking behaviour is not asserted, and why.

## Files

- `Genelife.Domain.Tests/Docs/DocsConsistencyTests.cs`
- `Genelife.Api.IntegrationTests/` (endpoint parity and admin smoke)
- `docs/plans/roadmap-v7/02-operator-plane/summary.md`

## Contracts

- Existing parity assertions keep passing unchanged.
- New coverage asserts the response shape and the accepted -> applied transition, not HTTP plumbing details.
- Uncovered behaviour is named, not implied.

## Acceptance

- Every migrated route is covered by a declaration assertion and a response-shape assertion.
- The accepted -> applied transition is covered for at least one migrated endpoint end to end.
- Any remaining unasserted blocking behaviour is listed with a reason.

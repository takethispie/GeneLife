# L01 — Hybrid Wolverine cleanup

**Phase:** 1 — Architecture
**Depends:** V5 features 01–04
**Estimate:** 2–3 sessions
**Status:** Planned (V5)

## Goal

Close the remaining hybrid ECS + Wolverine/Marten migration so V5 ships with one command, event, read and auth path instead of two.

## Context

The hybrid architecture is largely in place. H16 (hiring saga), H19 (remaining ASP.NET endpoints) and H20 (feature flags and parity verification) are still partial. Keeping both paths through V5 would multiply the surface that player, appearance, consumption and social features must support.

## Design

### H16 — Hiring saga

- Decide whether the hiring lifecycle is a Wolverine saga or remains collect → decide → apply.
- If it becomes a saga, the saga owns posting, application, offer, acceptance, expiry and seat release.
- The simulation tick remains the only writer to ECS state; the saga enqueues commands.
- If it stays collect → decide → apply, document why and remove the partial saga scaffolding.
- No duplicate hiring handler may exist.

### H19 — Remaining endpoints

- Move the remaining ASP.NET endpoints to Wolverine HTTP endpoints.
- Keep the existing routes, status codes and auth policies.
- Remove the old endpoint files only after parity tests pass.
- Player and admin routes added in V5 use the same endpoint style from the start.

### H20 — Feature flags and parity

- Inventory every `FeatureFlags` entry and its code paths.
- For each flag, either remove the old path and the flag or document why it stays.
- Add a parity test that runs the same command through the new path and compares the result.
- Remove dead registrations and duplicate DI wiring.
- Keep a rollback note for any flag that remains.

### Auth and ownership

- Admin and player authorization go through the same middleware/policy path.
- Ownership checks are not reimplemented in each endpoint.
- The old auth endpoint and the new endpoint must not disagree on token validation.

## Tasks

- [ ] Decide and document the H16 hiring path.
- [ ] Finish or remove the hiring saga scaffolding.
- [ ] Migrate the remaining endpoints to Wolverine HTTP.
- [ ] Add route/status/auth parity tests.
- [ ] Inventory and remove obsolete feature flags.
- [ ] Remove dead endpoint files and DI registrations.
- [ ] Add a migration checklist update.
- [ ] Run the full integration suite on the single path.

## Files

- `Genelife.API/Program.cs`
- `Genelife.API/Endpoints/*`
- `Genelife.API/Handlers/*`
- `Genelife.API/Program.cs`
- `docs/plans/hybrid-wolverine/migration-checklist.md`
- `Genelife.UnitTests/Integration/WolverineParityTests.cs`

## Acceptance

- Only one command path is active for each mutation.
- Only one read path is active for each read model.
- Player and admin auth use one policy path.
- Feature flags are removed or documented with a reason and a rollback note.
- The full PostgreSQL suite passes on the single path.
- No endpoint returns a different status or body than its predecessor without a documented change.

## Testing

- Run the endpoint parity suite.
- Run the command/event integration suite.
- Run the full PostgreSQL suite with all flags on and then with flags removed.
- Assert no duplicate handler registration.
- Assert the old endpoint files are gone or explicitly retained.

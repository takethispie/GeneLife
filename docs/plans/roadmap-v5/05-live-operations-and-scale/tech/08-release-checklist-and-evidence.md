# L08 — Release checklist and evidence

**Phase:** 8 — Release
**Depends:** L01–L07
**Estimate:** 1–2 sessions
**Status:** Planned (V5)

## Goal

Ship V5 without silently changing existing worlds, losing player or social state, or regressing the economy, performance or moderation posture.

## Checklist

The full checklist lives in [release-checklist.md](../release-checklist.md). This step is the execution and sign-off.

### Build and tests

- [ ] `dotnet build GeneLife.sln` succeeds.
- [ ] Full PostgreSQL suite passes.
- [ ] Player, appearance, consumption and social suites pass.
- [ ] 2,000-sim decade and 32-player soak pass.
- [ ] Balance and 30-year continuity pass.
- [ ] Migration and double-restore pass.
- [ ] Client typecheck, lint, unit, build and Playwright pass.

### Data and migration

- [ ] Schema 11 → 15 migration rehearsed on a real save copy.
- [ ] Appearance, player, utility, grocery and social state restore exactly.
- [ ] Chat retention does not block restore.
- [ ] No invented player family or social edges for legacy saves.
- [ ] `BalanceBackfilled`, `TraitsMigrated` and V4 migrations remain intact.
- [ ] Rollback build and pre-migration snapshot are available.

### Player life

- [ ] Job, housing, consumption, family, death and continuation work live.
- [ ] Player actions are server-authoritative and audited.
- [ ] `CohabitOnPairing` defaults to true only with green evidence.
- [ ] No player route exposes another player's private state.

### Appearance

- [ ] Every sim has a deterministic descriptor.
- [ ] Family resemblance and aging are visible.
- [ ] No per-sim texture is used.
- [ ] Payload and frame budgets pass.

### Consumption

- [ ] Utilities, outages, grocery and nutrition work live.
- [ ] Money and goods are conserved.
- [ ] The admin Consumption page works.

### Social

- [ ] Presence, chat, friends, blocks, reports and player partnerships work live.
- [ ] Moderation queue and retention work.
- [ ] The 32-player soak passes.
- [ ] No real names, emails or IP addresses are persisted.

### Admin

- [ ] `/player`, `/appearance`, `/consumption`, `/social` and `/family` load.
- [ ] All admin routes require the admin policy.
- [ ] Destructive actions require confirmation and are audited.
- [ ] Empty, seeded and migrated worlds render correctly.

### Performance

- [ ] 2,000-sim decade inside tick budget.
- [ ] 32-player soak inside command/frame budget.
- [ ] Appearance composition inside frame budget.
- [ ] Snapshot/delta payload inside budget.
- [ ] Projection lag inside the acceptable window.

### Documentation

- [ ] `STATUS.md` updated with evidence and known gaps.
- [ ] MP05 and hybrid status docs reconciled.
- [ ] Migration and rollback runbook documented.
- [ ] Release evidence complete.
- [ ] Open questions resolved or explicitly deferred.

### Release

- [ ] Release job passes.
- [ ] Staged deployment on a copy of production data.
- [ ] Live preview smoke after deploy.
- [ ] Monitoring for invariants, tick budget, projection lag and reports.
- [ ] Sign-off recorded with commit id and date.

## Rollback

- Keep the previous build and the pre-migration snapshot copy.
- A rollback restores the previous build and the saved schema 11 or pre-V5 state.
- Do not attempt to downgrade a schema 15 save in place.
- Chat and moderation documents are retained separately from the world snapshot.
- Record the rollback decision and reason in `STATUS.md`.

## Known gaps to carry forward

- Same-sex pairing and reproduction remain a policy decision.
- Divorce, custody, adoption and surrogacy remain deferred.
- A richer genome and genetic disease model remain deferred.
- Player-owned businesses, utilities and grocery chains remain deferred.
- Voice chat, groups and a social feed remain deferred.
- Wealth Gini, vacancy and company failures may remain outside target; record the final numbers.

## Tasks

- [ ] Execute the release checklist.
- [ ] Record every evidence artifact.
- [ ] Rehearse rollback.
- [ ] Write the release note with known gaps.
- [ ] Get sign-off.
- [ ] Update `roadmap-v5/STATUS.md` to release-ready.

## Files

- `docs/plans/roadmap-v5/05-live-operations-and-scale/release-checklist.md`
- `docs/plans/roadmap-v5/05-live-operations-and-scale/release-evidence.md`
- `docs/plans/roadmap-v5/STATUS.md`
- `README.md`

## Acceptance

- Every checklist item is checked or has a documented exception.
- Every claim points to a test, scenario report or live smoke.
- Migration and rollback are rehearsed.
- The release note lists known gaps.
- Sign-off is recorded.
- No invariant is knowingly failing at release.

## Testing

- Run the full release gate.
- Run the preview smoke.
- Run the migration and rollback rehearsal.
- Review the evidence bundle.
- Confirm the previous build is deployable.

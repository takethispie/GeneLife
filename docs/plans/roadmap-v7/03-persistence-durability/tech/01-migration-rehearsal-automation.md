# Migration Rehearsal Automation

## Context

The V5 release checklist carries a manual schema 11 -> 15 migration/rollback rehearsal. It is a release step rather than a check, which means it happens when someone remembers, runs against whatever state the developer's database is in, and produces evidence only if someone records it.

V1 -> V5 moved the schema from 4 to 15 and V6 added none, so the next schema change is the next time this matters. Automating it now means the next change is rehearsed by default.

## Design

1. **Exercise forward and back against a real database.** Apply the migration, assert the read-back shape, roll back, assert the original shape. The existing restore coverage (legacy snapshots, the ten-year scenario) is the assertion material, so the rehearsal should reuse it rather than build a parallel fixture.
2. **Run from a known state.** The value of the rehearsal is that it does not depend on the developer's database. Start from a seeded baseline, not from whatever is lying around, so a pass means something.
3. **Fail with the schema version in the message.** "Migration 11 -> 15 failed at step X" is actionable; "test failed" is not.
4. **Hook it to the change, not the release.** Run it when persistence changes, and make it cheap enough to keep there.
5. **Update the checklist.** Once automated, the V5 checklist entry becomes a pointer to the check rather than an instruction to perform it by hand.

## Tasks

- [ ] Identify the migration path and the rollback path for the current schema to a seeded baseline.
- [ ] Write the rehearsal as a check: migrate, assert, roll back, assert.
- [ ] Reuse the existing restore fixtures rather than creating new ones.
- [ ] Ensure it starts from a seeded baseline and fails with the schema version and step in the message.
- [ ] Wire it to run when persistence changes.
- [ ] Update the V5 release checklist entry to point at the automated check.

## Files

- `Genelife.Api.IntegrationTests/` (persistence and schema coverage)
- `docker-compose` PostgreSQL service definition
- `docs/plans/release-roadmap-V5/README.md`
- `docs/plans/roadmap-v5/STATUS.md`

## Contracts

- The rehearsal is repeatable from a seeded baseline and does not depend on developer database state.
- Forward and rollback are both asserted, not just forward.
- Failure messages name the schema version and the failing step.

## Acceptance

- The rehearsal runs as part of the standard checks and passes on master.
- Deliberately breaking the rollback path makes the rehearsal fail with a message naming the schema version.
- The V5 checklist points at the automated check instead of instructing a manual run.

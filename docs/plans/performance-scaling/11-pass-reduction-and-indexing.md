# Step 11 — Pass reduction and indexed lookups

**Depends:** 10
**Goal:** Cut redundant full-world scans and O(N) repository lookups.

**Status:** Done

## Tasks

- [x] Identify systems scanning overlapping archetypes per tick.
- [x] Combine hot passes or move to chunk iteration where it pays.
- [x] Replace `Get`/`Delete`/`GetAll` scans with `NetworkId`→entity maps.
- [x] Measure per-pass cost before/after.
- [x] Document the final system/pass layout.

## Files likely touched

- `Genelife/Systems/*`
- `Genelife.Api/Repositories/*`

## Acceptance

- Measured reduction in passes or lookup cost.
- Behavior unchanged (existing tests pass).

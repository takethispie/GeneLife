# Step 11 — Pass reduction and indexed lookups

**Depends:** 10
**Goal:** Cut redundant full-world scans and O(N) repository lookups.

## Tasks

- [ ] Identify systems scanning overlapping archetypes per tick.
- [ ] Combine hot passes or move to chunk iteration where it pays.
- [ ] Replace `Get`/`Delete`/`GetAll` scans with `NetworkId`→entity maps.
- [ ] Measure per-pass cost before/after.
- [ ] Document the final system/pass layout.

## Files likely touched

- `Genelife/Systems/*`
- `Genelife.Api/Repositories/*`

## Acceptance

- Measured reduction in passes or lookup cost.
- Behavior unchanged (existing tests pass).

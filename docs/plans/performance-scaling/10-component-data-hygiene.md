# Step 10 — Component data hygiene

**Depends:** 08
**Goal:** Reduce GC and copying from reference types inside components.

## Tasks

- [ ] Inventory components with `string`/`List<T>` fields.
- [ ] Replace `JobPosting.Requirements` and `JobSeeker.Skills` with IDs/tokens or side tables.
- [ ] Keep hot components blittable where practical.
- [ ] Measure allocation and snapshot serialization before/after.
- [ ] Migrate tests and repositories.

## Files likely touched

- `Genelife/Components/Employment/*`
- `Genelife/Systems/HiringSystem.cs`
- `Genelife.Api/Repositories/*`

## Acceptance

- Hot components contain no `List<T>`.
- Allocation/GC and serialization cost measurably reduced.

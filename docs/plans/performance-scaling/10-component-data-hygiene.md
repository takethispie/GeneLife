# Step 10 — Component data hygiene

**Depends:** 08
**Goal:** Reduce GC and copying from reference types inside components.

**Status:** Done

## Tasks

- [x] Inventory components with `string`/`List<T>` fields.
- [x] Replace `JobPosting.Requirements` and `JobSeeker.Skills` with IDs/tokens or side tables.
- [x] Keep hot components blittable where practical.
- [x] Measure allocation and snapshot serialization before/after.
- [x] Migrate tests and repositories.

## Files likely touched

- `Genelife/Components/Employment/*`
- `Genelife/Systems/HiringSystem.cs`
- `Genelife.Api/Repositories/*`

## Acceptance

- Hot components contain no `List<T>`.
- Allocation/GC and serialization cost measurably reduced.

# Step 04 — De-tick HiringSystem

**Depends:** 03
**Goal:** Take the quadratic hiring work off the every-tick path.

## Context

`DecideApplications` is seekers × postings, runs every 40 ms, and copies components into
fresh lists each tick.

## Tasks

- [ ] Run hiring on a warm/cold cadence (e.g., every 1–5 s) or on events (new posting/application).
- [ ] Index postings by company; early-exit when no applications/seats.
- [ ] Avoid re-copying `JobPosting`/`JobSeeker` if unchanged; cache IDs.
- [ ] Add a tick counter/metric for hiring duration.
- [ ] Verify outcomes match the old behavior.

## Files likely touched

- `Genelife/Systems/HiringSystem.cs`
- `Genelife.Api/Services/SimulationEngine.cs`

## Acceptance

- Hiring no longer appears in hot-path top consumers.
- Hire/reject/seats-filled behavior is unchanged.

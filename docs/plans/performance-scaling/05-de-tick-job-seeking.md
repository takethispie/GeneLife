# Step 05 — De-tick JobSeekerSystem

**Depends:** 04
**Goal:** Stop scanning all non-employed Sims every tick for a slow-changing condition.

## Tasks

- [ ] Run on a cold cadence (aging changes slowly).
- [ ] Use collect → apply (also fixes structural safety).
- [ ] Track the last processed age/eligibility to skip work.
- [ ] Add a metric for scans and additions.

## Files likely touched

- `Genelife/Systems/JobSeekerSystem.cs`
- `Genelife.Api/Services/SimulationEngine.cs`

## Acceptance

- Job-seeking cost is off the hot path.
- Each eligible Sim becomes a seeker exactly once.

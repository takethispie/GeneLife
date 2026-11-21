# Step 09 — Telemetry and CI guardrails

**Depends:** 08
**Goal:** Prevent performance regressions from landing unnoticed.

## Tasks

- [x] Emit per-system timing and allocation metrics.
- [x] Define thresholds per NPC tier (see plan thresholds).
- [x] Add a CI or scheduled benchmark job using the load harness.
- [x] Fail/warn on regressions beyond a tolerance.
- [x] Publish a short report per run.

## Files likely touched

- `.github/workflows/*`
- `Genelife.Api/Services/Metrics.cs`

## Acceptance

- A regression in tick p95 or allocation fails/warns CI.
- Baseline is stored and compared.

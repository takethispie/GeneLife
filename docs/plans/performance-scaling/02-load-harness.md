# Step 02 — Load harness

**Depends:** 01
**Goal:** One command that produces a performance report.

**Status:** Done

## Tasks

- [x] Add deterministic NPC fixtures at fixed counts (1k/5k/10k).
- [x] Support synthetic (all active) and realistic (mostly idle) workloads.
- [x] Run a fixed number of ticks and capture Step 01 metrics.
- [x] Output a machine-readable report for regression comparison.
- [x] Document how to run it.

## Files likely touched

- new `Genelife.UnitTests/Performance/*` or a console tool
- `Genelife.Api/Services/*`

## Acceptance

- One command yields a stable report across runs.
- Results feed CI thresholds (Step 09).

# L07 — 1k–2k balance and performance

**Phase:** 7 — Verification
**Depends:** L04–L06, feature 03 recalibration
**Estimate:** 3 sessions
**Status:** Done with documented exceptions (V5 feature 05) — 1k rent burden miss and 18 distinct-household overcrowding warnings; cohabitation before/after recorded

## Goal

Close or explicitly document the V1 1k–2k balance gaps and prove V5 performance at 2,000 sims and 32 players.

## Context

V1's known limitations list vacancy, company failures, wealth Gini and wage share as open at 1k–2k. V4's balance pass fixed money leaks and pension capitalization but did not close those targets. V5 adds player jobs, utilities, grocery and cohabitation, which change household budgets again.

Performance evidence exists for 2,000 sims in V4, but V5 adds appearance, consumption and social work. It must be re-measured.

## Design

### Balance report

Record for 1k and 2k populations over 10 years:

- wage share;
- vacancy;
- company failure rate;
- wealth Gini;
- money supply and conservation;
- household utility and grocery cost;
- overcrowding and health warnings;
- birth/death/replacement rate.

For each metric, record the target, the result and whether it is a blocker. A known miss is documented, not hidden.

### Tuning

- Tune within the existing bands and the closed money loop.
- Do not mint money to fix household solvency.
- Do not relax a band to make the report pass.
- Prefer demand, construction, turnover and consumption levers over a single coefficient.
- Re-run the 30-year continuity scenario after any tuning.

### Performance

- 2,000-sim decade with appearance, utilities, grocery and family.
- 32-player soak with movement, chat, social and player actions.
- Record tick p50/p95/p99, allocation per tick, snapshot/delta bytes, frame time and composition cache size.
- Record the worst system and the command drain cost.
- Compare to the V4 baseline and the existing budgets.

### Evidence

- Balance JSON and a readable report.
- Performance JSON and a readable report.
- Before/after table for cohabitation and V5 features.
- Known gaps and the decision to ship or block.

## Tasks

- [x] Add the 1k and 2k balance report.
- [x] Record the V4 baseline and the V5 result.
- [ ] Tune demand, construction, turnover and consumption. No tune was made because the only band miss is 1k rent burden; hidden regressions would not be visible without a separate tuning change.
- [x] Re-run the 30-year continuity scenario.
- [x] Run the 2,000-sim decade performance scenario.
- [x] Run the 32-player social soak.
- [x] Record tick, allocation, payload and frame numbers.
- [x] Write the known-gaps and ship/block decision.

## Files

- `Genelife.UnitTests/Performance/BalanceScenarioTests.cs`
- `Genelife.UnitTests/Performance/ContinuityScenarioTests.cs`
- `Genelife.UnitTests/Performance/FamilyPerformanceTests.cs`
- `Genelife.UnitTests/Performance/SocialSoakTests.cs`
- `docs/plans/roadmap-v5/05-live-operations-and-scale/release-evidence.md`

## Acceptance

- The 1k and 2k balance reports are recorded with targets and results.
- Every known miss is documented with a reason and a follow-up.
- The 30-year continuity run has no new `Fail`.
- The 2,000-sim decade stays inside the tick budget.
- The 32-player soak stays inside the command and frame budgets.
- The before/after cohabitation table is recorded.
- The ship/block decision is explicit.

## Testing

- 10-year balance at 1k and 2k.
- 30-year continuity at 200 sims and a shorter 2k run.
- 2,000-sim decade.
- 32-player soak.
- Restore and replay the tuned world.
- Compare against the V4 baseline.

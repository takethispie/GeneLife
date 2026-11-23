# C05 — Long-run population balance

**Phase:** 5 — Balance
**Depends:** C01–C04
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Run the population long enough to prove births, deaths, immigration, households and inheritance produce a stable society rather than a collapse or a baby boom.

## Context

The 2026-11 balance pass produced `BalanceScenarioTests`: 200 sims, 10 years, yearly economy/housing/population snapshots, conservation and target bands. V4 adds births, family households and inheritance, so the same harness must include them.

## Design

### Scenarios

| Scenario | Population | Years | Purpose |
|---|---|---|---|
| Family decade | 200 | 10 | births, school, work, household formation |
| Family generation | 200 | 30 | replacement, inheritance, dependency ratio |
| Family scale | 2,000 | 10 | performance and distribution |
| Immigration-only baseline | 200 | 10 | compare with the pre-V4 path |

### Target bands

| Indicator | Target |
|---|---|
| Population | within ±20% of start, or growing with a documented reason |
| Births per year | > 0 and not a runaway |
| Replacement rate | 0.8–1.3 over a generation |
| Children share | 15–35% |
| Average household size | 2.0–4.0 |
| Orphans without a guardian | 0 |
| Minor trust conservation gap | 0 |
| Overcrowded units | < 15% |
| Child arrears | visible but not universal |
| Pension funding ratio | ≥ 0.8 |
| Money conservation gap | 0 |
| Unemployment | 4–10% |
| Wealth Gini | tracked; the V1 target may remain unmet |

Bands are reviewed after the first run; a miss is documented with a cause, not hidden by changing the target silently.

### Levers

- `MonthlyConceptionChance`;
- `SeedPartnershipShare`;
- child cost scaling;
- immigration target and pause thresholds;
- school capacity and enrollment;
- housing construction response to family size.

### Output

Extend the balance report with:

- births, deaths, arrivals and natural growth;
- children by age band;
- households by size;
- orphan and state-care counts;
- inheritance and trust totals;
- per-year family metrics.

### Determinism

- The same seed and options reproduce births, pairings and inheritance.
- A 10-year replay comparison is mandatory.
- A 30-year run may be a slow test or a manual report; document which.

## Tasks

- [ ] Extend `BalanceScenarioTests` or add `FamilyScenarioTests` with births enabled.
- [ ] Add the 30-year generation scenario.
- [ ] Add the 2,000-sim family scenario.
- [ ] Add family fields to the report.
- [ ] Tune conception, costs, housing and immigration.
- [ ] Record the results in the balance-target doc.
- [ ] Add a replay comparison.

## Files

- `Genelife.UnitTests/Economy/BalanceScenarioTests.cs`
- new `Genelife.UnitTests/Family/FamilyScenarioTests.cs`
- `docs/plans/roadmap-v1/01-economy/business/04-balance-targets.md`
- `docs/plans/roadmap-v4/STATUS.md`

## Acceptance

- The family decade produces births and a stable population.
- The 30-year run does not collapse or explode.
- Conservation and minor trust gaps are zero.
- Orphans always have a guardian or state care.
- The 2,000-sim run stays inside the tick budget.
- The same seed reproduces the family report.

## Testing

- Family decade assertions.
- 30-year generation smoke.
- 2,000-sim performance.
- Replay comparison.
- Existing economy/housing balance tests remain green.
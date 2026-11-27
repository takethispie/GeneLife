# Roadmap V7 — Status

**Status:** Track definition in progress; no track started. This page records delivered state as it lands, and starts as the carry-in record for everything V7 must close.
**Builds on:** V1–V6 delivered state, the V5 balance bands, the V6 Release measurements, gabrielo's band guardrails (fast tier + performance tier)
**Scope:** economic closure and balance bands (spine), operator plane completion, persistence durability
**Primary risk:** the bands are coupled. Rent burden, vacancy, Gini and housing pressure move together through consumption timing, benefits and rent collection, so a change that fixes one band can push another out — the `ede655b` rebalance is the worked example, since neither of its two fixes passes the bands alone. Rebalance against the gate, one measured change at a time.

## What is already true (so track 1 is scoped honestly)

Track 1 is **not** "build the gate". The bands are already asserted:

- `AssertBalanceBands` — rent burden (<= 0.40) and Gini (<= 0.70), at **every** scale.
- `AssertTargetBands` — unemployment, homelessness, pension funding, wage share and vacancy, at the reference scale of 200.
- `BalanceScenarioTests` also asserts conservation: sink delta 0, conservation gap 0, money supply growth <= 0.10, at every scale.
- Gabrielo's band guardrails were recently moved from `[Skip]` to a hard assert (board update), so the fast and performance tiers now fail rather than skip.

What is actually open is narrower, and it is where track 1's work is:

1. **Three bands have no target at all** — company failure rate, health warnings, reproduction warnings. A band with no target is a report row, not an acceptance criterion.
2. **The record around the gate rots.** On a master where the bands are green, `BalanceScenarioTests` still carried comments saying rent burden and Gini "currently MISS the V5 bands" and "**EXPECT THIS RED** until pedro's rent-burden rebalance lands" — false since `ede655b`, and undetected by anything. A reader is told to expect a red that never arrives, which is how a real red gets ignored later. Corrected in this branch; the second, larger point is that nothing prevents the next instance. This is the same defect class already fixed for code references and for the snapshot DTOs.
3. **Reconciling a band failure costs a bisect.** The assertion message names the value but not the report file, and the report holds a per-year array while the assertion reads the final year (`report.Years[^1]`), which is easy to misread — reading the first year and comparing it against the message manufactures a false "assertion and report disagree" conclusion.

## Band state at V7 start

Sources are named because these numbers do not all come from the same harness.

| Band | Population / seed | Target | Value at V7 start | Source |
|---|---|---|---|---|
| Rent burden | 60 / 200 / 1k / 2k, seed 21 | <= 0.40 | 0.2093 / 0.2917 / 0.3359 / 0.3446 | `ede655b` |
| Wealth Gini | 60 / 200 / 1k / 2k, seed 21 | <= 0.70 | 0.50 / 0.55 / 0.57 / 0.57 | `ede655b` |
| Unemployment rate | 200, final year | <= 0.10 | asserted green | `AssertTargetBands` |
| Homeless households | 200, final year | 0 | asserted green | `AssertTargetBands` |
| Pension funding ratio | 200, final year | 0.80 – 2.00 | asserted green | `AssertTargetBands` |
| Wage share | 200, final year | 0 – 1.0 | asserted green | `AssertTargetBands` |
| Housing vacancy rate | 200, final year | <= 0.12 | asserted green | `AssertTargetBands` |
| Money supply growth | every scale | <= 0.10 | asserted green | `BalanceScenarioTests` |
| Conservation gap / sink delta | every scale | 0 | asserted green | `BalanceScenarioTests` |
| Company failure rate | 200 | **no target** | 0 reported | balance report |
| Health warnings | 2k, 10 years | **no target** | 61 warnings | V4 STATUS |
| Reproduction warnings | 200, 30 years | **no target** | 4 `Warn` levels | V4 STATUS |
| Vacancy / overcrowding under `CohabitOnPairing` | 200, 30 years | <= ~20% / 0 | 17.7% -> 26.6% vacancy, overcrowded 3 -> 9 | V4 STATUS |

## Track 1 — Economic closure and balance bands (spine)

| Area | Status | Evidence |
|---|---|---|
| Targets for the untargeted bands | Not started | Company failure rate, health warnings and reproduction warnings get numeric targets and assertions |
| Band failure messages name band, report and aggregation | Not started | A failing band identifies its report file and the aggregate used, so reconciling needs no bisect |
| No knowingly-red assertions | Not started | No test carries an "expected red" annotation; an unmeetable band is a tracked item |
| Band narrative kept true | In progress | Stale rent-burden/Gini comments corrected in this branch; the general guard is not built |
| Rent burden and Gini inside band | Done (`ede655b`) | 0.2093/0.2917/0.3359/0.3446 and 0.50/0.55/0.57/0.57 at 60/200/1k/2k; `BalanceScenarioTests` 5/5 on freshly fetched master |
| Retire `CohabitOnPairing` | Not started | Shipped or deleted, with the 30-year vacancy/overcrowding numbers in band if shipped |
| Retire cadence staggering | Not started | Opt-in path deleted; decision record kept |

## Track 2 — Operator plane completion

| Area | Status | Evidence |
|---|---|---|
| Legacy admin endpoint migration | Not started | `202 Accepted` + `/api/commands/{id}`; no endpoint waits on the next tick |
| `RelocateBuildingCommand` follow-through | Not started | V2's deferred item; confirm whether anything remains beyond the shipped path |
| Endpoint parity and admin smoke | Not started | Moved routes covered by parity tests |

## Track 3 — Persistence durability

| Area | Status | Evidence |
|---|---|---|
| Migration/rollback rehearsal automation | Not started | Runs automatically instead of as a manual V5 release-checklist step |
| Read-model staleness guards | Not started | Guard fails when a projection or read-model field stops being replicated, extending the `DtoGuard` approach |

## Open items carried into V7

| Item | Origin | State at V7 start |
|---|---|---|
| `CohabitOnPairing` vacancy 17.7% -> 26.6%, overcrowded 3 -> 9 | V4 | Opt-in, unresolved since V4 |
| Cadence staggering moves bands (0.4369 vs 0.40) | V6 | Opt-in, decided stays off, switch not removed |
| 61 health warnings (2k, 10 years) | V4 | Non-blocking, no target, unexplained |
| 4 reproduction `Warn` levels (30 years) | V4 | Non-blocking, no target, unexplained |
| Legacy admin endpoints wait up to 50 ms | V6 | Open |
| 1k–2k tuning: vacancy, company failures | V1 | Open since V1; wealth Gini now in band |
| Company failure rate has no target | balance report | Reported as 0, never asserted |
| Manual schema migration/rollback rehearsal | V5 release checklist | Manual |

## Fixed just before V7 (recorded, not open)

| Item | Was | Now | Commit |
|---|---|---|---|
| Rent burden, seed 21, 60/200/1k/2k | 0.4619 / 0.4041 / 0.4619 / 0.4619 | 0.2093 / 0.2917 / 0.3359 / 0.3446 | `ede655b` |
| Wealth Gini, seed 21, 60/200/1k/2k | 0.76 / 0.78 / 0.77 / 0.79 | 0.50 / 0.55 / 0.57 / 0.57 | `ede655b` |
| `BalanceScenarioTests` | 3 failures | 5 / 5 green (re-measured on freshly fetched master) | `ede655b` |

The regression bisected to `5ddc300` ("run the monthly consumption pass after income") and was fixed by two coupled changes in `ede655b`: discretionary spending no longer consumes the household's rent obligation, and `HousingOptions.MaxRentIndex` moved 1.6 -> 1.2 as a market-wide lever. `5ddc300` **stays** — it fixed household purchase gating.

## Verification plan

| Check | When |
|---|---|
| Band gate green at 60/200/1k/2k, documented seeds | Every change to economy, housing, consumption, benefits or rent |
| `BalanceScenarioTests` including the 200/21 case | Every change in track 1 |
| No "expected red" annotation in the suite | Every change in track 1 |
| Determinism and replay suites | Every change in any track |
| Money conservation and schema suites | Every change touching transfers or persistence |
| Endpoint parity and admin smoke | Every change in track 2 |
| Migration/rollback rehearsal | Every change in track 3, and before any schema bump |
| `DocsConsistencyTests` documentation guard | Every change under `docs/` |

## Definition of done

Every band has a target and a green assertion; no knowingly-red assertion or stale band narrative survives; a band failure is traceable to its report without a bisect; zero opt-ins remain; no admin endpoint blocks on the tick; the migration rehearsal and read-model guards are automated. See [`README.md`](README.md) for the track definitions.

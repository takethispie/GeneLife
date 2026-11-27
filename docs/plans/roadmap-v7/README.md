# Roadmap V7 — Candidate tracks

**Status:** definition in progress (2026-11-26). The three candidate tracks below are ranked from
@main-dev's backend/infra input. Track 1 is the intended **spine**; tracks 2 and 3 are secondary.
See [ROADMAPS.md](../ROADMAPS.md) for the series index.

**Scope:** server simulation, economy, persistence and the admin/operator plane. **Not a performance
roadmap** (see below).

**Builds on:** V1–V6 delivered state, the V5 balance bands, the V6 Release measurements, and
gabrielo's band guardrails (PR fast tier + perf tier).

---

## What V7 is NOT — performance

V6 left roughly **5× headroom**: the 2,000-genos gate runs p95 1.97 ms and the 5k synthetic fixture
10.8 ms, both against a 50 ms budget, with allocation flat. Scale is not the binding constraint, and
both perf opt-ins died on second contact — cadence staggering measured as noise in Release, and
parallel per-entity passes were deleted for leaking foreground threads. **Another performance
roadmap would be inventing work.**

---

## Track 1 — Economic closure and balance bands  *(spine)*

The largest documented debt, and it spans four roadmaps:

- **V1 open** — 1k–2k tuning of vacancy, company failures and wealth Gini.
- **V4 open** — `CohabitOnPairing` recalibration (30-year run: vacancy 17.7% → 26.6%, overcrowded
  units 3 → 9).
- **V5 open** — the 1k rent-burden miss. *State correction:* rent burden and Gini were brought back
  inside the bands by the rent-burden rebalance (`ede655b`, `HousingOptions.MaxRentIndex` 1.6 → 1.2);
  the regression that caused them bisected to `5ddc300`.
- Plus **61 health warnings** on the 2k ten-year run and **4 reproduction `Warn` levels** at 30 years.

**Deliverable:** the bands become a **checked gate** (gabrielo's guardrails), the numbers land **in
band**, and the two carried opt-ins either ship or are deleted. Highest value because it unblocks
features already built and not shipping.

## Track 2 — Operator plane completion

V6's own open item: the legacy admin endpoints block up to 50 ms waiting for the next tick — move
them to the `202 Accepted` + `/api/commands/{id}` pattern the newer endpoints already use. Pair it
with V2's deferred `RelocateBuildingCommand`. This is the difference between live ops that can be
interrupted and one that cannot, and the pattern already exists in-repo, so it is **mechanical rather
than inventive**.

## Track 3 — Persistence durability

V1→V5 took the schema from 4 → 15, and the V5 release checklist still contains a **manual**
migration/rollback rehearsal. Automate that rehearsal, and extend guard-style completeness checks to
the read models — the silent-stale-DTO bug (`CompaniesEqual`) was found by hand, and Marten
projections carry the same silent-staleness risk. V6 added no schema change, so V7 is the natural
place.

---

## Explicitly retired in V7

Every "still opt-in" marker must end V7 as either **shipped or deleted, with the reason recorded**:
`CohabitOnPairing` (V4/V5) and cadence staggering (V6 — decided: stays off). An opt-in that survives
two roadmaps is unpriced risk.

## Invariants V7 must not break

Per the cross-roadmap themes: **server authority** (the browser never mutates the world); **determinism**
(no `Random.Shared`, stable iteration order, replay tests); **money conservation** (integer-cent
`Money`, ledgered transfers); **collect → decide → apply**; **admin-first observability**; **schema
discipline**.

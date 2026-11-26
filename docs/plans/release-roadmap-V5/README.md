# Release Roadmap V5 — Production Release Checks

**Purpose:** this is where V5's production release checks live. Feature roadmaps
(`docs/plans/roadmap-vN`) record delivered implementation only — they do not carry
production sign-off. Every production release gate for a version belongs in its
companion `release-roadmap-V{n}`.

**Companion to:** [`roadmap-v5/`](../roadmap-v5/README.md) — Playable Society.
Implementation status is recorded in [`roadmap-v5/STATUS.md`](../roadmap-v5/STATUS.md).

**Scope:** release evidence, rollback rehearsal and sign-off only. No feature
implementation. Outstanding implementation work stays in the V5 feature roadmap;
this roadmap only proves the version is safe to ship.

**Status:** V5 features are implemented (see the V5 status page). Production release
is gated on the checks below — none are signed off yet.

---

## Release gate checklist

| # | Gate | Evidence to record | Status |
|---|---|---|---|
| R1 | 30-minute live preview soak | HUD input-to-ack p95 and `/api/simulation/metrics` sampled continuously over 30 minutes on a named build | Pending |
| R2 | 32-player social soak at release | No dropped ticks attributable to lock contention or thread-pool starvation; presence/chat/social assertions clean | Pending |
| R3 | 2,000-genos decade at release | p95/p99 inside the 50 ms tick budget; health warnings triaged (18 `housing.overcrowded` in the family release run) | Pending |
| R4 | Schema 11 → 15 migration and rollback rehearsal | Forward migration, restore, double restore and rollback against a pre-V5 backup, all against a named build | Pending |
| R5 | Feature 02 appearance acceptance | Playwright visual smoke + reference-hardware frame-budget report | Pending |
| R6 | Feature 03 consumption acceptance | NPC shopping-trip/meal acceptance + 30-year cohabitation before/after comparison | Pending |
| R7 | Release evidence record | Single record populated with every R1–R6 result and reviewer sign-off | Pending |

## Documented carry-forward items

- **1k rent burden miss.** At 1,000 genoses median rent burden is 0.4284 against a
  0.40 ceiling (2,000 genoses: 0.3464). Documented, not blocking.
- **`CohabitOnPairing` stays opt-in.** The 30-year 200-genos comparison (population
  199 → 219, births 75 → 83, but vacancy 17.7% → 26.6% and overcrowded units 3 → 9)
  currently argues against flipping it. Revisit only after economy/consumption
  recalibration.
- **Non-blocking signal:** 61 health warnings in the 2k ten-year run; 4 reproduction
  `Warn` levels in the 30-year run.

## Definition of done

- R1–R6 each have a recorded result against a named build.
- The release evidence record (R7) is populated and signed off by a reviewer.
- No blocking regression against the V5 feature acceptance: 560 domain / 69 API /
  33 performance-release tests and 178 browser tests green.
- The documented carry-forward items are either resolved or explicitly accepted.

---

Relates to: [`roadmap-v5/`](../roadmap-v5/README.md),
[`ROADMAPS.md`](../ROADMAPS.md).

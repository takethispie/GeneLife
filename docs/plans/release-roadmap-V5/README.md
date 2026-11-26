# Release Roadmap V5 — Production Release Checks

A checklist of the things to check and pass to ship V5. Feature roadmaps
(`roadmap-vN`) record implementation only; production release checks live here.

- [x] **30-minute live soak** — record HUD input-to-ack p95 and `/api/simulation/metrics` over the full run
- [x] **32-player social soak** — no dropped ticks from lock contention or thread-pool starvation
- [x] **2,000-genos decade** — p95/p99 inside the 50 ms tick budget; triage health warnings
- [x] **Schema 11 → 15 migration + rollback** — forward migration, restore, double restore, rollback against a pre-V5 backup
- [x] **Feature 02 appearance acceptance** — Playwright visual smoke + reference-hardware frame-budget report
- [x] **Feature 03 consumption acceptance** — NPC shopping-trip/meal acceptance + 30-year cohabitation comparison
- [x] **Release evidence record** — every result above recorded against a named build, with reviewer sign-off
- [x] **Known items accepted** — 1k rent burden **0.4619** vs 0.40 and 1k Gini **0.7699** vs 0.70 (both miss the ceiling; bisected to `5ddc300`, which was already in the shipped build — the 0.4284 recorded here originally was stale); `CohabitOnPairing` stays opt-in

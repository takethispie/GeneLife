# Release Roadmap V5 — Production Release Checks

A checklist of the things to check and pass to ship V5. Feature roadmaps
(`roadmap-vN`) record implementation only; production release checks live here.

- [ ] **30-minute live soak** — record HUD input-to-ack p95 and `/api/simulation/metrics` over the full run
- [ ] **32-player social soak** — no dropped ticks from lock contention or thread-pool starvation
- [ ] **2,000-genos decade** — p95/p99 inside the 50 ms tick budget; triage health warnings
- [ ] **Schema 11 → 15 migration + rollback** — forward migration, restore, double restore, rollback against a pre-V5 backup
- [ ] **Feature 02 appearance acceptance** — Playwright visual smoke + reference-hardware frame-budget report
- [ ] **Feature 03 consumption acceptance** — NPC shopping-trip/meal acceptance + 30-year cohabitation comparison
- [ ] **Release evidence record** — every result above recorded against a named build, with reviewer sign-off
- [ ] **Known items accepted** — 1k rent burden 0.4284 vs 0.40 (documented miss); `CohabitOnPairing` stays opt-in

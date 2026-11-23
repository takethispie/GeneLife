# Live Operations and Scale — Summary

**Folder:** `05-live-operations-and-scale/`
**Status:** Planned (V5 feature 05)

**Release artifacts:** [release checklist](release-checklist.md) · [release evidence](release-evidence.md)

**Depends on:** features 01–04, V1–V4 release state, V3 client, hybrid ECS/Wolverine architecture
**Feeds:** release, operations, live preview, post-V5 planning

## Objective

Ship V5 as an operable live world, not a collection of features. Close the remaining hybrid architecture work, reconcile stale status documents, finish the deferred world and client work, prove the 1k–2k balance and 32-player load, deploy a seeded preview and record release evidence.

This feature is the release gate. If the live preview, migration, balance or performance evidence is missing, V5 is not ready.

## Baseline before V5

- Hybrid ECS + Wolverine/Marten is in place; H16 (hiring saga), H19 (remaining endpoints) and H20 (feature flags/parity) are partial.
- MP05 is marked partial in docs even though `SimulationEngine.DrainPlayerIntents()` applies player intents.
- Occupied-building relocation is deferred from V2.
- `CommuteMode.Animate` is opt-in and needs a pathfinding-budget measurement.
- V3's live preview acceptance, hardware render numbers, audio, touch controls and prediction decision remain open.
- V1's 1k–2k balance gaps remain open: vacancy, company failures, wealth Gini and wage share.
- V4's `CohabitOnPairing` follow-up is addressed by feature 03.
- V5 features 01–04 add player, appearance, consumption and social state through schema 15.

## Deliverables

1. Hybrid Wolverine cleanup: H16, H19, H20 closed and feature flags removed.
2. Command/event/status reconciliation so docs match the code.
3. Occupied-building relocation and default commute animation.
4. Live preview deployment and live browser acceptance.
5. Minimal audio cues and a touch/mobile control scheme.
6. An evidence-based prediction decision and optional implementation.
7. 1k–2k balance report and 2,000-sim / 32-player performance evidence.
8. Release checklist, migration runbook, rollback plan and release evidence.

## Architecture

```
Feature 01–04 code
   │
   ├── hybrid cleanup ──► one command/event/read path
   ├── status reconciliation ──► docs match code
   ├── relocation/commute ──► world completeness
   ├── audio/touch/prediction ──► client polish
   └── balance/performance ──► release evidence
                                   │
                                   ▼
                          seeded live preview
                                   │
                                   ▼
                     release checklist + evidence
```

The release path is evidence-driven: every claim in the checklist points to a test, a scenario report or a deployed smoke test.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Hybrid Wolverine cleanup](tech/01-hybrid-wolverine-cleanup.md) | Architecture |
| 02 | [Command, event and status reconciliation](tech/02-command-event-and-status-reconciliation.md) | Architecture |
| 03 | [Building relocation and commute animation](tech/03-building-relocation-and-commute-animation.md) | World |
| 04 | [Live preview and browser acceptance](tech/04-live-preview-and-browser-acceptance.md) | Release |
| 05 | [Audio and touch controls](tech/05-audio-and-touch-controls.md) | Client |
| 06 | [Prediction decision and implementation](tech/06-prediction-decision-and-implementation.md) | Client |
| 07 | [1k–2k balance and performance](tech/07-1k-2k-balance-and-performance.md) | Verification |
| 08 | [Release checklist and evidence](tech/08-release-checklist-and-evidence.md) | Release |

## Business reading

- [Shipping a live world](business/01-shipping-a-live-world.md)
- [Learning from players](business/02-learning-from-players.md)
- [What comes after V5](business/03-what-comes-after-v5.md)

## Definition of done

- H16, H19 and H20 are closed or explicitly deferred with a reason, and feature flags are removed or documented.
- MP05 and all roadmap status docs match the code.
- An occupied building can be relocated without losing residents, employees, students or path caches.
- `CommuteMode.Animate` is enabled by default or has a recorded budget reason not to.
- A seeded preview deployment passes the live Playwright suite and hardware render numbers are recorded.
- Audio cues and touch controls are available and can be disabled.
- The prediction decision is recorded with latency evidence; any implementation is measured against the no-prediction baseline.
- The 1k–2k balance report and the 2,000-sim / 32-player performance evidence are recorded.
- Schema 11 → 15 migration, restore, double restore and rollback are rehearsed.
- The release checklist is complete and the release evidence is signed off.

# Performance & Scaling — Step Index

**Status:** Steps 01–11 done; step 12 is trigger-gated and not activated.

Keep one authoritative world responsive as NPC count and system count grow.

## Order

```mermaid
flowchart TD
    P01[01 baseline ✅] --> P02[02 load harness ✅]
    P02 --> P03[03 zero-alloc decisions ✅]
    P03 --> P04[04 de-tick hiring ✅]
    P04 --> P05[05 de-tick job seeking ✅]
    P05 --> P06[06 snapshot reads ✅]
    P06 --> P07[07 harden tick loop ✅]
    P07 --> P08[08 registry + cadence ✅]
    P08 --> P09[09 telemetry + CI ✅]
    P09 --> P10[10 data hygiene ✅]
    P10 --> P11[11 pass reduction ✅]
    P11 --> P12[12 triggered scaling ⏸]
```

Legend: ✅ done · ⏸ trigger-gated (not activated)

## Rules

- No optimization without a Phase-0 baseline.
- Land steps 03–07 before any spatial/AOI/parallelism work.
- Steps 12 items are trigger-gated; do not start them speculatively.

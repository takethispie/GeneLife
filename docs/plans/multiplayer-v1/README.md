# Multiplayer V1 — Step Index

**Status:** Steps 01–04 and 06–14 done; step 05 is partial (command intents not drained).

One world, server-authoritative, up to 32 concurrent players. No instances or lobbies.

## Order and dependencies

```mermaid
flowchart TD
    S01[01 structural safety ✅] --> S02[02 employment truth ✅]
    S01 --> S04[04 tick loop ✅]
    S03[03 network ids ✅] --> S06[06 snapshots ✅]
    S04 --> S05[05 command queue 🟡]
    S05 --> S06
    S07[07 identity ✅] --> S08[08 player vs npc ✅]
    S08 --> S09[09 authorization ✅]
    S06 --> S10[10 signalr ✅]
    S09 --> S10
    S10 --> S11[11 delta + reconnect ✅]
    S03 --> S12[12 2d + spatial ✅]
    S06 --> S12
    S06 --> S13[13 persistence ✅]
    S11 --> S14[14 acceptance ✅]
    S12 --> S14
    S13 --> S14
```

Legend: ✅ done · 🟡 partial

## Rules

- One step = one PR-sized change; do not merge without its acceptance criteria and tests.
- Steps 01–02 are prerequisites; they fix shared-world corruption.
- Steps 03–06 build the internal server loop before any client is connected.
- Transport (10–11) is added only after the server can produce snapshots.
- Persistence (13) is required before calling V1 done.

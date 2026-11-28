# 08 — Technical Architecture & Constraints

> **Status: Outline — co-authored with @tech-leader.**

## 8.1 Intent
Record the **hard technical facts** a feature must respect, so design decisions are
implementable and do not fight the architecture.

## 8.2 To capture (tech-leader)
- [ ] **Server-authoritative topology**: processes, the tick loop, the write lock, intent
      drain, and where a client sits in the flow.
- [ ] **The wire contract**: `Genelife.Contracts` / `Genelife.Messages` — snapshots, deltas,
      intents, the three replication scopes (static map / world delta / scoped interior).
- [ ] **Determinism & replay**: named `SimulationRandom` streams, stable ordering, replay
      tests; what a new system must do to stay deterministic.
- [ ] **Persistence & schema**: current schema version, migration/rollback discipline,
      read-model projections.
- [ ] **The desktop client**: Godot **C#** (not GDScript), **desktop-only**, sharing the wire
      contract but **not** the domain/application assemblies; the net10.0 spike to prove
      GodotSharp can load the sim's target framework.
- [ ] **The every-feature observability rule**: read models + endpoints + admin surface ship
      with the feature.
- [ ] **Performance envelope**: the tick budget and the ~5× headroom at 2k (recorded as
      context; no new perf work at the current stage).

## 8.3 Known constraints already decided
- No client-authoritative simulation (V3 rule) — the desktop client must not reference
  `Genelife.Domain`/`Genelife.Application`.
- C# cannot export to web in Godot 4 — hence the desktop-only client and the browser target
  staying PixiJS.
- Any new per-genos replicated field must be added to `WorldSnapshotDelta.GenosesEqual` or it
  silently never replicates.

---
*Co-author invitation: @tech-leader — please draft §8.2 as the authoritative technical input.*

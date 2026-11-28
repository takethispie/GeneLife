# 09 — Roadmaps Ahead

> **Status: Draft v0.4 — authored by @tech-leader (co-author).** Supersedes the v0.1 outline.
> **Verified against `origin/develop` @ `2f978eb` and the team board, 2026-11-28.**
> This chapter is the *game-design* view of the plan; the **authoritative delivery plan stays in
> `docs/plans/`** per the release-roadmap convention. Nothing here opens a roadmap, a card or a
> workstream — it maps this GDD onto the series and states the dependencies, so the PO can choose.

---

## 9.1 Where the project actually is

Read from the roadmap index, the V7 status page and the board — not from memory.

| Roadmap | Theme | State |
|---|---|---|
| V1 | Realistic population simulation (economy, housing, life stages) | Implemented |
| V2 | Spatial world, buildings, player presence | Implemented |
| V3 | Browser game client (PixiJS + SignalR) | Implemented (vertical slice) |
| V4 | Relationships, attraction, reproduction, inheritance | Release-ready |
| V5 | Playable society (player life, appearance, consumption, social) | Released |
| V6 | Tick performance | Implemented |
| V7 | Economic closure & balance bands · operator plane · persistence durability | **Defined; no track started** |
| release-roadmap-V5 | V5 production release checks | Released |

**Two standing constraints bound everything below.**

1. **Balance and performance work is not a current priority** (PO ruling). V7's spine is economic
   closure, so **V7 is not the vehicle for the phases below** — a GDD phase becomes its own roadmap
   entry (V8+) when the PO opens it, and no phase here schedules balance or perf work. The 32000
   streaming work is the one apparent exception, and it is not one: it is **feasibility and
   correctness** (without it the world cannot be generated), not optimisation.
2. **World sizing is atomic.** Building footprints, lot pitch and map extent move together or
   generation throws. This is a correctness gate, not a preference about batching work.

**Where the live work already sits** (board, 2026-11-28): one card `ready` — the desktop client
adopting the canonical chunk protocol; two `blocked` — the interiors footprint/world-size scoping, and
a firm-entry mechanism on the economy side. The client repo (`GeneLife.Desktop`) has its scaffold and
its CI landed; its city map exists on a feature branch.

---

## 9.2 The phases this GDD implies

Five phases, each stated as: what it delivers, the technical gate that proves it, and what it becomes
in `docs/plans/`.

### Phase A — Client foundation (the bridge)

**Delivers:** the Godot C# desktop client reaching parity on the play loop — render the sim's world
*from the contract*, own the HUD, send intents. Server-side, the world-scale migration's **phases 1–3**
land **while the default is still 256**: pure-function generator (already landed, #111), sparse index
and AOI bounds, and the viewport/chunk snapshot protocol.

**Gate:** the client renders the sim's world at 256 with **no client-side generation** and no
hardcoded extent, and the four required checks stay green throughout.

**Becomes:** a `roadmap-vN` bridge entry (client foundation + viewport protocol), with the client's own
repo carrying its own checks.

**Why 1–3 before the size flip:** flipping `MapOptions` to 32000 before the generator is pure-function
and the snapshot is viewport-scoped makes generation throw or OOM — a 1 GB tile array and a 2 GB
signature string. The ordering is the correctness gate.

### Phase B — World and space (atomic)

**Delivers:** properly sized building footprints, the lot pitch that follows, and the map extent
(32000×32000) — **in one change** — plus persistence of the world as a **spec** (seed + options +
placements + reservations) with tiles regenerated on demand. Schema `16 → 17`.

**Gate:** the sim generates and persists a 32000² world without materialising it; fixtures, golden
tests and visual baselines are regenerated in the same change.

**Becomes:** the world-scale migration's phase 4 (+5), as its own roadmap track. **Depends on A's
server half.**

### Phase C — Interiors and personal space

**Delivers:** the first interiors — **player-only, private first**, roof cutaway on the same map. The
scoped `InteriorPlan` request and the replicated `InteriorLocation` (§8.5(3)). The web client renders
**generic templates per building kind**; the Godot client renders **procedurally from tilesets** — two
renderers, one model, one contract.

**Gate:** a player enters a building, sees its rooms, and acts inside it, with the interior plan never
riding the world delta.

**Becomes:** the interiors roadmap (the dossier in `docs/research/building-interiors/` is its plan
input). **Depends on B** — a cutaway is an index over the building's own footprint, so a room cannot
be bigger than a footprint that is currently an envelope sized to content.

### Phase D — Control and UX (the Sims layer)

**Delivers, server half:** the **action catalogue** read model, **structured cause payloads**, the
**day-ribbon schedule/record** read models and the **digest grouping projection** (§8.5 items 1, 2, 4,
6). **Client half:** the intent dial, the soma ring, the day ribbon, the town bell and the dollhouse on
the desktop client (chapter 07).

**Gate:** every verb the dial shows is a contract verb, every number on the play surface carries a
server-stated cause, and the client contains no rule the server does not own.

**Becomes:** a personal-control roadmap entry, split **server read models first, client surfaces
second** — the split is real and worth two entries.

### Phase E — Social depth

**Delivers:** shared interiors (interiors V2), NPC interior access (explicitly **performance-gated** by
the PO's Q1 ruling — a separate feature, not a widening of Phase C), and player-to-player depth beyond
the V5 base.

**Becomes:** the social roadmap, after the above. **Depends on C** for anything spatial.

---

## 9.3 The dependency graph, and the one ordering recommendation

```
A-server (generator ✅ · sparse index · viewport protocol) ─┐
                                                           ├─→ B (sizing + extent, atomic) ─→ C (interiors) ─→ E (social)
A-client (Godot shell · HUD · chunk cache) ────────────────┘            │
                                                                        ▼
D-server (catalogue · causes · digest) ──────────────────────→ D-client (dial · ribbon · bell · dollhouse)
        ▲ no dependency on A or B
```

**The recommendation I would put to the PO: start Phase D's server half independently of the world
work.** The action catalogue, the cause payloads and the digest grouping are read models over state the
simulation **already has** — `LastMealTick`, balances, ledger entries, `PlayerActionKind`, the existing
event feed. They need no footprint, no bigger world and no new map protocol, and they are the cheapest
thing that unblocks the two flagship screens (the dial and the dollhouse) that chapter 07 is designed
around. Coupling the UX workstream to the 32000 migration would put the most player-visible part of
this GDD behind the largest and riskiest technical change in it.

The converse is also worth stating: **Phase D's client half needs Phase A.** The dial and the dollhouse
are desktop-client surfaces; the read models can land first and be exercised through the operator
plane, but the player sees nothing until the client exists.

---

## 9.4 Constraints on sequencing (kept from v0.1, extended)

1. **No balance or performance work** is scheduled by any phase above (standing PO ruling).
2. **World sizing is atomic** — footprints + lot pitch + extent together, or generation throws.
3. **The chunk constant and the snapshot message shape are decided once, in `Genelife.Contracts`,
   before the protocol phase.** It is the only genuinely irreversible choice in the migration: pick it
   wrong and the protocol and both clients are redone.
4. **Feature roadmaps record delivered implementation only**; production sign-off lives in
   `release-roadmap-V{n}`.
5. **No feature flags.** `DocsConsistencyTests` fails the build if `FeatureFlags` reappears, so a
   dual-path transition must be expressed as config + protocol versioning.
6. **Every phase carries its own verification tier**, and the four required checks stay green — a phase
   that needs a red to make progress is not ready to be a phase.
7. **Docs that carry a number move with the number.** The 32000 migration has a named doc surface
   (`world-scale-migration-32000.md` §3.7, the client's own `city-map` note, the roadmap index); a
   half-migrated doc set is how the next reader gets a wrong answer.

---

## 9.5 What this chapter does not decide

- **Which phase starts, and when.** That is the PO's call; this chapter maps the GDD onto the roadmap
  series and names the dependencies.
- **The roadmap numbering.** Whether these become V8, V9, V10 or tracks inside a smaller number of
  roadmaps is a `docs/plans/` decision, made when a phase is opened.
- **Chunk size, world extent and the shape of the interior payload** are recorded here as decided
  inputs, not re-opened. Changing one of them re-does the phases that consume it.

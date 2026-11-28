# 10 — Risks & Open Questions

> **Status: Draft v0.5 — authored by @roberto.**

## 10.1 Identity risks (protect the pillars)
| Risk | Why it matters | Mitigation |
|---|---|---|
| **Scope drift into a generic sandbox** | Loses the Dwarf-Fortress depth that differentiates GeneLife | Every feature must strengthen a pillar or be cut (§1.3) |
| **The UI hides the simulation** | Deep simulation becomes a spreadsheet | Pillar-3 legibility; adriano owns the read-out |
| **The bridge becomes a reboot** | Reworking the model wastes the mature V1–V5 asset | Treat model+contract+GDD as durable; only the renderer changes |
| **Single-player drift** | Undermines the shared-world pillar | Design server-first; multiplayer in the model, not bolted on |
| **Interiors/perf collision** | Room-level simulation at population scale breaks the tick budget | Player-only interiors; NPCs stay footprint-presence |

## 10.1.1 Delivery risks (sequencing)

| Risk | Why it matters | Mitigation |
|---|---|---|
| **Coupling the UX read models to the world-scale migration** | Puts the most player-visible part of the GDD (the dial, the dollhouse) behind the largest, riskiest technical change | Start Phase D's **server read models** independently — they are read models over state the sim already has and need no bigger world (chapter 09 §9.3) |
| **Chunk constant / snapshot message fixed too late** | The one genuinely irreversible choice; getting it wrong re-does the protocol and both clients | Decide the chunk constant and snapshot shape **once**, in `Genelife.Contracts`, before the protocol phase (chapter 09 §9.4) |
| **A half-migrated doc set** | A number or path that moves in code but not in docs sends the next reader to a wrong answer | Docs that carry a number move with the number (chapter 09 §9.4; e.g. the schema 15→16 correction) |

## 10.2 Open questions (to resolve)
- [ ] **World size:** how big? (Scale direction landed; the *number* is the PO's call.)
- [ ] **Building sizing:** minimum-viable vs comfortable — confirm minimum-first for V1.
- [ ] **Shared households:** what two partnered players share mechanically (see §6.4).
- [ ] **Heir continuation:** how legacy selection works across players and NPCs.
- [ ] **Creation — no face editor (PO ruling needed):** chapter 07's design position is that
      character creation has **no face editor** — appearance is *derived* from traits/age/sex,
      so the player chooses a life, not a look. Follows the brief and §5.5, but is a
      product-level position, not styling.
- [ ] **Death — household continuation (PO ruling needed):** the design position is that death
      **continues the household as an heir** rather than restarting the playthrough (§5.5,
      mockup `08-life-legacy`). Needs explicit confirmation.
- [ ] **Combat/conflict:** confirm anti-goal (no ballistic combat) holds for all visions.
- [ ] **Client parity bar:** what "the desktop client reaches parity" must include to end
      the bridge.

## 10.3 Standing rulings this GDD must respect
- Balancing & performance work is **deprioritised** at the current stage.
- The browser client is a **stepping stone**; the desktop client is the real renderer.
- Desktop client is **Godot + C#**, **desktop-only**.

---
*Maintained by @roberto; risks added as they surface.*

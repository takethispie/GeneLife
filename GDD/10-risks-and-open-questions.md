# 10 — Risks & Open Questions

> **Status: Outline (risks seeded).**

## 10.1 Identity risks (protect the pillars)
| Risk | Why it matters | Mitigation |
|---|---|---|
| **Scope drift into a generic sandbox** | Loses the Dwarf-Fortress depth that differentiates GeneLife | Every feature must strengthen a pillar or be cut (§1.3) |
| **The UI hides the simulation** | Deep simulation becomes a spreadsheet | Pillar-3 legibility; adriano owns the read-out |
| **The bridge becomes a reboot** | Reworking the model wastes the mature V1–V5 asset | Treat model+contract+GDD as durable; only the renderer changes |
| **Single-player drift** | Undermines the shared-world pillar | Design server-first; multiplayer in the model, not bolted on |
| **Interiors/perf collision** | Room-level simulation at population scale breaks the tick budget | Player-only interiors; NPCs stay footprint-presence |

## 10.2 Open questions (to resolve)
- [ ] **World size:** how big? (Scale direction landed; the *number* is the PO's call.)
- [ ] **Building sizing:** minimum-viable vs comfortable — confirm minimum-first for V1.
- [ ] **Shared households:** what two partnered players share mechanically (see §6.4).
- [ ] **Heir continuation:** how legacy selection works across players and NPCs.
- [ ] **Combat/conflict:** confirm anti-goal (no ballistic combat) holds for all visions.
- [ ] **Client parity bar:** what "the desktop client reaches parity" must include to end
      the bridge.

## 10.3 Standing rulings this GDD must respect
- Balancing & performance work is **deprioritised** at the current stage.
- The browser client is a **stepping stone**; the desktop client is the real renderer.
- Desktop client is **Godot + C#**, **desktop-only**.

---
*Maintained by @roberto; risks added as they surface.*

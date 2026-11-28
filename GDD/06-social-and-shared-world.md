# 06 — Social & Shared World

> **Status: Draft v0.5 — authored by @roberto.**
> The town is **shared**: many players inhabit one persistent, server-authoritative
> simulation. Interaction is **social, not adversarial** — the multiplayer layer amplifies the
> life-sim (Pillar 4); it does not turn GeneLife into a competitive game.

## 6.1 Why the town is shared

Pillar 4 exists so that a *life* is lived among *other lives* — the player's household is one
of many, and the society does not shrink to a single protagonist. Concretely:

- **Continuity with company:** the world keeps running between sessions, and other players are
  part of that continuity.
- **Social texture:** friendships, partnerships, conversations and shared places give the
  personal life (Pillar 3) an audience and a network.
- **A real society:** the same systems that generate NPC society (chapter 04) host players, so
  a player's choices sit inside an economy and demography that already have their own momentum.

## 6.2 What exists today (V5 — grounded)

The social layer is **implemented**, not aspirational:

| Capability | What it is |
|---|---|
| **Presence** | Players and their genoses are visible and replicated in the shared world. |
| **Chat & emotes** | Text conversation between players in the town. |
| **Friendships** | A persistent social graph between players. |
| **Blocks & reports** | Player-controlled safety: hide from, or report, another player. |
| **Consensual partnerships** | Two players' genoses can partner — the relationship is *consensual* by design. |
| **Venues** | Places that exist for social gathering. |
| **Moderation** | Moderation notices and the tooling to act on reports. |

These are exposed through the **operator/admin plane** as well as the play surface, per the
admin-first observability invariant (chapter 02 §2.2, invariant 5).

## 6.3 Design boundaries (binding)

1. **Players never control another player's genos.** Each player owns their genos and household;
   the town and its society are shared. This is the line that keeps Pillar 3 and Pillar 4
   compatible — *one shared simulation, many private lives* (chapter 05 §5.6).
2. **Partnerships are consensual and modelled as relationships**, not as a claim of ownership.
3. **The society is player-count-independent.** The simulation runs identically with zero or
   many players online; players join a living town rather than starting one.
4. **Safety is first-class.** Blocking and reporting are in the model, not bolted on — a shared
   world without player-controlled safety is not shippable.
5. **No adversarial PvP spine.** Conflict, where it exists, is social and economic, not
   ballistic (chapter 01 §1.5, anti-goal 1).

## 6.4 Social surfaces (interface)

The social layer is one surface in the design language (chapter 07 §7.2 / mockup
`06-people-conversation`): *players and residents in one list, marked by a chip*, where a
conversation produces **intents** (the same contract-inbound model as everything else). "Is a
player" is a replicated, authoritative fact (`IsPlayerControlled`), never inferred client-side —
see chapter 08 §8.5 item 7.

## 6.5 Open questions

- [ ] **Shared households:** what do two partnered players *share* mechanically — a home, money,
      children, a lineage? (The V5 base makes them partners; the depth beyond that is open.)
- [ ] **Venues:** authored places, or places players can create/claim?
- [ ] **Reputation / standing:** is there a social-capital model, or only raw relationships?
- [ ] **Scale:** how do player count and NPC count trade off inside a session/tick budget?
      *(Performance work is out of scope at the current stage — recorded here as a design
      question, not scheduled.)*
- [ ] **Shared interiors:** the V2 interior ruling (private first, shared later) lands in the
      social roadmap — see chapter 09, Phase E.

---
*These questions are the PO's to rule on; this chapter records the decided boundaries and
leaves the depth open rather than inventing it.*

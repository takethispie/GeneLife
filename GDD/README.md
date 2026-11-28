# GeneLife — Game Design Document (GDD)

> **Status:** Draft v0.9 — living document, open for revision.
> **Owner / compiler:** @roberto (Solution Researcher). Technical chapter co-owned with @tech-leader; UI/UX chapter co-owned with @adriano.
> **Branch:** `feature/gdd` (targets `develop`).
> **Location:** `/GDD` at the repository root.

This document is the **bible of what GeneLife is**. It is normative: when code, roadmap,
UI or marketing disagree with this document, either the code is wrong or this document is
out of date — the two must be reconciled, and the reconciliation is a revision recorded in
the log below. It is written to survive the current client transition (the PixiJS browser
client → a desktop client) and the roadmaps ahead.

---

## 1. What this document is for

| Reader | Use it for |
|---|---|
| Product owner | The single statement of the game's identity and target experience; the reference to arbitrate feature requests against. |
| Tech lead / engineers | The system map, the invariants, and the technical constraints a feature must respect. |
| UI/UX | The design language, screen inventory and control model that all mockups must serve. |
| Researchers / future contributors | The grounding: what already exists, what is decided, what is open. |

**How to read it.** Start with [01 — Vision & Pillars](01-vision-and-pillars.md) to know
what the game *is*. Then [02 — Current State & The Bridge](02-current-state.md) to know
where the project actually is today. Everything after that is the detail bible.

**How to change it.** Every substantive change is a **revision**: bump the version, add a
row to the Revision Log (section 4), and describe what moved. Decisions that change the
game's identity must cite the product owner's ruling.

---

## 2. Table of contents

| # | Chapter | Status | Primary author |
|---|---|---|---|
| 00 | [Glossary & Naming Conventions](00-glossary.md) | Draft | roberto |
| 01 | [Vision & Pillars](01-vision-and-pillars.md) | Draft | roberto |
| 02 | [Current State & The Bridge](02-current-state.md) | Draft | roberto |
| 03 | [World, Space & Buildings](03-world-and-space.md) | Draft | roberto |
| 04 | [Simulation Core](04-simulation-core.md) | Draft | roberto |
| 05 | [The Player & Control Model](05-player-and-control.md) | Draft | roberto |
| 06 | [Social & Shared World](06-social-and-shared-world.md) | Draft v0.5 | roberto |
| 07 | [UI/UX Design Language & Screens](07-ui-ux.md) | Draft v0.4 + [mockups](mockups/index.html) | adriano |
| 08 | [Technical Architecture & Constraints](08-technical-architecture.md) | Draft v0.4 | tech-leader (co-author) |
| 09 | [Roadmaps Ahead](09-roadmaps.md) | Draft v0.4 | tech-leader (co-author) |
| 10 | [Risks & Open Questions](10-risks-and-open-questions.md) | Draft v0.5 | roberto |
| 11 | [Design Vignettes](11-design-vignettes.md) | Draft | roberto |

**Target length:** ≥ 20 printed pages when complete. Chapters marked *Draft* are written;
*Outline* chapters carry their structure and open asks and are filled as contributors land.

---

## 3. Design summary (one paragraph)

GeneLife is a **top-down, 2D, persistent-life simulation** — a small town of hundreds to
thousands of autonomous *genoses* (NPCs) that earn, spend, rent, age, form families, fall
into poverty and die inside a **closed, conserved economy**, rendered on an **open, freely
explorable tile world**, where a player **directs their own genos** through an ordinary
life: work, home, family, money and social ties. Think **Dwarf Fortress' simulation depth**
living inside **GTA's open spatial freedom** (flattened to 2D) with **The Sims' personal
control over your character(s)** — a *"make your own life"* simulation that a shared,
server-authoritative town can host for many players at once.

---

## 4. Revision Log

| Ver | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-28 | roberto | Initial fork: index, vision & pillars, current state, world/space. Chapters 06–10 outlined. |
| 0.2 | 2026-11-28 | adriano | Chapter 07 written: design language “Civic Paper”, six ratified principles, five interaction models, nine mockups under `GDD/mockups/`, interface→simulation mapping for chapter 08. |
| 0.3 | 2026-11-28 | roberto | Folded chapter 07 into the compiled doc (fast-forward of PR #113 into `feature/gdd`); verified the gallery and world-HUD mockups render; added §11.7 fiction↔interface mapping; surfaced two PO positions (no face editor, household continuation) in chapter 10. |
| 0.4 | 2026-11-28 | tech-leader | Chapters 08 and 09 written (were outlines): the technical spine — topology, the three replication scopes, determinism, schema 16, the desktop client, the per-feature constraint checklist — plus **answers to chapter 07 §7.4 items 1–7** (action catalogue as a server read model, structured cause payloads, the scoped interior request, the day ribbon's committed/scheduled/recorded layers, bundled fonts + HUD scale, the digest grouping key, presence via `IsPlayerControlled`), and the phase/dependency view of the roadmaps ahead. Header version corrected from the stale v0.1. |
| 0.5 | 2026-11-28 | roberto | Folded chapters 08/09 (fast-forward of PR #114 into `feature/gdd`); **corrected the schema number 15 → 16** (`PersistedWorldState.cs:5`) in chapter 02; completed chapters **06 (Social & Shared World)** and **10 (Risks)** from outline to draft and added §10.1.1 delivery-sequencing risks. All chapters are now Draft; the bible is whole (only the PO rulings in §10.2 remain open). |
| 0.6 | 2026-11-28 | adriano | Chapter 07 v0.3: §7.4 closed against §8.5 (seven items, two change hands), contrast **re-measured and the palette darkened where it failed its own rule** (labels 3.2:1 → 5.5:1, money 2.9:1 → 5.3:1, accent 4.0:1 → 5.2:1), §7.6 client acceptance checks (AC-1…AC-6) and a seventh binding anti-pattern added; mockups re-tinted to the measured tokens. |
| 0.7 | 2026-11-28 | roberto | Folded chapter 07 v0.3 (PR #115) into the compiler branch after #112 landed on `develop`; ran `contrast_check.py` first-hand (**15/15 pass**). **Fixed a self-contradiction in chapter 10:** the world size was ruled **32000×32000** (`world-scale-migration-32000.md` §0) but §10.2 still listed it as open — it and the **interiors rulings** (player-only, private-first, web-generic/Godot-procedural) now sit in §10.3 as standing rulings that ch.08/09 depend on. |
| 0.8 | 2026-11-28 | roberto (recording adriano & tech-leader) | PR #117 landed: the Civic Paper palette rule moved out of a standalone script into a **required CI check** — `ContrastTokenTests` (`Genelife.Domain.Tests/Docs`, `Category=Documentation`) parses the tokens from `mockups.css` and asserts the 15 pairs on every `unit` run; ch.07 **AC-6** and `GDD/mockups/README.md` now state that **CI is the guarantee** and the script is the local loop. Verified first-hand post-merge: `ContrastTokenTests` **3/3** under the CI filter and `Category=Documentation` **10/10**. |
| 0.9 | 2026-11-28 | roberto (recording adriano) | PR #120 landed: the contrast rule is **single-sourced** in `GDD/mockups/assets/contrast-pairs.json` (read by both the CI test and the local script — no second copy of the pair table), and the rule file is **itself guarded** by `The_rule_cannot_be_softened_or_emptied` (≥ 15 pairs; thresholds pinned to 4.5 / 7 per §7.5.2). Chapter 07 §7.6 **splits AC-4**: **AC-4a** = server parity (catalogue ⇄ `Evaluate`, including `refusalCode`, over every `PlayerActionKind` × fixture states), **AC-4b** = the client renders it faithfully. Verified first-hand: `ContrastTokenTests` **4/4**, `Category=Documentation` **11/11**, script **15/15**, and mutation-tested (threshold 7 → 3 fails the guard; revert clean). |
| 0.10 | 2026-11-28 | adriano | Chapter 07 v0.4: **§7.2.3 refusal is a reason, not a boolean** — the closed reason set, four rules (code selects presentation, unknown code = version skew, canonical strings frozen, mapping is consumer-side and per-surface) — with `GDD/mockups/assets/refusal-reasons.json` as the one source and `RefusalMappingTests` deriving the action path's code set rather than counting it. Budgeted for the Phase D catalogue card's acceptance criteria. |

---

## 5. Grounding and provenance

This GDD is **grounded in the shipped project**, not invented. Where it states what exists,
it reflects the V1–V6 roadmaps and the shared GeneLife memory; where it states what the game
*is meant to be*, it cites the product owner's ruling. External references (Sims, GTA, Dwarf
Fortress) are cited as **design lineage**, not as assets or mechanics to copy.

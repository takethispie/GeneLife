# GeneLife — Game Design Document (GDD)

> **Status:** Draft v0.1 — living document, open for revision.
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
| 06 | [Social & Shared World](06-social-and-shared-world.md) | Outline | roberto |
| 07 | [UI/UX Design Language & Screens](07-ui-ux.md) | Draft v0.2 + [mockups](mockups/index.html) | adriano |
| 08 | [Technical Architecture & Constraints](08-technical-architecture.md) | Outline | tech-leader (co-author) |
| 09 | [Roadmaps Ahead](09-roadmaps.md) | Outline | tech-leader (co-author) |
| 10 | [Risks & Open Questions](10-risks-and-open-questions.md) | Outline | roberto |
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

---

## 5. Grounding and provenance

This GDD is **grounded in the shipped project**, not invented. Where it states what exists,
it reflects the V1–V6 roadmaps and the shared GeneLife memory; where it states what the game
*is meant to be*, it cites the product owner's ruling. External references (Sims, GTA, Dwarf
Fortress) are cited as **design lineage**, not as assets or mechanics to copy.

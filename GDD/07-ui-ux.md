# 07 — UI/UX Design Language & Screens

> **Status: Outline — co-authored with @adriano (UI/UX expert).**
> **Explicit instruction from the product owner:** mockups for this document must be
> **new designs serving the desktop client**, *not* a copy of the current web game client's
> layouts ("not just copypasta of what is currently in the web game client").

## 7.1 UX principles (proposed — adriano to ratify)
1. **Legible depth.** The town is deeply simulated; the UI's job is to make it *readable*,
   never to hide it behind abstraction.
2. **Person-first.** The default view is the *player's life* — needs, home, money, family —
   with the society as context, not a dashboard of global metrics.
3. **Desktop-native.** Mockups assume a keyboard + mouse desktop (Godot client), not a
   responsive web page.
4. **Diegetic where possible.** Home, workplace, shop and school should be *places you go*,
   not *menus you open*.
5. **Quiet by default.** Emergent floods (notifications, events) must be summarised, not
   shouted — a lesson from the prototype's notification pressure.

## 7.2 Screen / surface inventory (to design)
| Surface | Purpose | Mockups needed |
|---|---|---|
| **World view (HUD)** | The default play view; player genos, town, context | main view, zoom levels, selection |
| **Genos panel** | The player's character state: needs, status, money | full + compact |
| **Household / family view** | Home, family members, shared resources, lineage | tree + household sheet |
| **Job & work view** | Employment, shift, career, income | browse/apply/manage |
| **Consumption & budget** | Groceries, utilities, spending | shopping + household budget |
| **Social view** | Players, chat, friendships, partnerships | directory + conversation |
| **Building / interior view** | Entering and using a place (cutaway) | interior frame + interaction |
| **Life & legacy view** | Aging, retirement, death, **heir continuation** | life timeline + successor |
| **Character creation / birth** | Appearance and starting life | creation + newborn |

## 7.3 What to avoid (anti-patterns from the prototype)
- Copying the web HUD's DOM-panel layouts wholesale.
- A global-admin dashboard as the *player's* default screen.
- Turning needs/economy into raw number tables.

## 7.4 Deliverable for this chapter
- A **design language** section (type, colour, spacing, iconography, motion) — *adriano*.
- **Mockups** for the surfaces in §7.2 — *adriano*, stored under `GDD/mockups/` with this doc
  referencing them.
- A short **rationale** per surface: what decision the player makes there.

---
*Co-author invitation: @adriano — please lead §7.4 and ratify/adjust §7.1.*

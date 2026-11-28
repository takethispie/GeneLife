# GDD mockups — the desktop client, drawn

Companion artifact to chapter [07 — UI/UX](../07-ui-ux.md).

Every file here is **self-contained HTML** — open it directly in a browser (no build step, no server,
no network). Each screen is drawn inside a fixed **1440×900** desktop frame, so what you see is the
target window, not a responsive reflow.

Start at **`index.html`** for the gallery (live thumbnails of all nine screens).

## Index

| File | Surface | Owning chapter |
|---|---|---|
| `00-design-language.html` | Design language “Civic Paper” — tokens, components, motion, accessibility, anti-patterns | 07 §7.2 |
| `01-world-hud.html` | World view & HUD — life card, town time, Town bell, day ribbon, intent dial | 07 §7.2.2 (a)(b)(c)(d) |
| `02-genos-sheet.html` | Genos sheet — needs with cause lines, inherited traits, ties | 05, 07 |
| `03-household-lineage.html` | Household & lineage — roster, weekly budget, house cutaway, family tree, heir | 05 §5.5 |
| `04-work-career.html` | Work & career — net pay, ladder as a gate map, job board, applications | 05 §5.3 |
| `05-market-budget.html` | Market & budget — shelves, basket, four-week projection | 05 §5.3 |
| `06-people-conversation.html` | People & conversation — players + residents, chat that produces intents | 06 |
| `07-interior-cutaway.html` | Interior cutaway — dollhouse plan, object affordances, room state | 03 §3.4 |
| `08-life-legacy.html` | Life & legacy — the life as a record, projections, heir choice | 05 §5.5 |
| `09-new-life.html` | A new life — born or immigrant, inherited traits, derived appearance | 05, 07 |

`assets/mockups.css` holds the design tokens and component kit; `assets/town.js` is a small procedural
renderer that draws the mock town and the interior floor plan. Both are mockup-only: the real renderer is
the Godot/C# desktop client, and nothing here is a proposal for client-side simulation.

## Conventions used by the mockups

- **Money** is always integer cents, formatted `₡ 1 284.40` (thin space grouping), mono, tabular.
- **Time** is town time; dates read `Tue 14 Mar`, ages `31 y 204 d`.
- **Every number has a cause.** Bars carry a one-line explanation in the simulation's own terms.
- **Every button states its consequence** ("Pay at the till · ₡ 40.25"), never "Confirm".
- **Disabled things explain themselves** ("needs a store", "requires Endurance 5 · you: 3 ✗").
- Persona used throughout: *Mira Voss*, 31, packer at Vell &amp; Sons Cannery, 24 Kestrel Row,
  partner Rafe, child Fenn — one household, so the screens can be read as one life.

## What these are not

- Not a port of the current PixiJS web client's layouts or palette (see chapter 02 §2.3.1 and 07 §7.6).
- Not final art. Sprites, portraits and icons are placeholders for the design language; the geometry and
  hierarchy are the deliverable.
- Not a second UI specification for the admin surfaces — those stay as they are.
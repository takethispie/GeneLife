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

## Verification

The palette is checked, not asserted — and checked **in CI**, not by memory.

- **CI (authoritative):** `ContrastTokenTests` in `Genelife.Domain.Tests/Docs/` carries
  `Category=Documentation`, which means it runs inside the required `unit` check. A palette
  regression fails the merge. It includes a permanent negative control (`The_check_can_fail`,
  using the v0.2 label colour at 3.23:1) so the assertion cannot silently become vacuous.
- **Local (fast loop):** the same rule as a script, for editing the palette without a build:

```
python3 GDD/mockups/assets/contrast_check.py    # 15/15 pairs pass; exit 1 on regression
```

**Refusal vocabulary** is the same shape: `assets/refusal-reasons.json` maps the canonical wire codes
onto the UI's closed reason set (`surface` + `producer` per entry), and `RefusalMappingTests` derives the
action path's code set from the two services rather than counting it, so a new `Reject("…")` fails until
someone decides what the player is told. Chapter 07 §7.2.3 is the rule; the JSON is the machine-readable copy.

**One copy of the rule, two readers.** The *colours* live in `assets/mockups.css`; the *rule*
(pairs, thresholds, purpose) lives in `assets/contrast-pairs.json`. The script and the CI test both
read those same two files, so the designer's loop and the merge gate cannot disagree. Add a pair to
the JSON and both check it; lower a threshold and `The_rule_cannot_be_softened_or_emptied` fails.

The first token set **failed** this check (labels 3.2:1, money 2.9:1); the colours were darkened
until it passed, rather than the thresholds being relaxed. See chapter 07 §7.5.1.

## What these are not

- Not a port of the current PixiJS web client's layouts or palette (see chapter 02 §2.3.1 and 07 §7.7).
- Not final art. Sprites, portraits and icons are placeholders for the design language; the geometry and
  hierarchy are the deliverable.
- Not a second UI specification for the admin surfaces — those stay as they are.
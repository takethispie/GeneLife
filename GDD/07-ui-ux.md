# 07 — UI/UX Design Language & Screens

> **Status: Draft v0.4 — authored by @adriano (UI/UX).** Supersedes the outline in v0.1.
> **Contract status:** all seven interface→simulation questions are answered in chapter 08 §8.5 (§7.4 below);
> the remaining items are the two product-level rulings parked in chapter 10 §10.2.
> **Product owner's instruction stands:** these are **new desktop-client designs**, not a port of the
> PixiJS web client's layouts. The web HUD's palette, panel-per-subsystem structure and DOM chrome are
> explicitly *not* carried over; only its UX *decisions* (what the player can do) survive the bridge.
>
> **Companion artifact:** `GDD/mockups/` — nine surfaces plus a design-language sheet, as self-contained
> HTML at a 1440×900 desktop frame. Open `GDD/mockups/index.html` for the gallery, and
> `GDD/mockups/00-design-language.html` for the token sheet. The town and interior views in them are
> procedural *mockup* renderers; they are not the Godot client.

| # | Surface | File | Decision the player makes there |
|---|---|---|---|
| 00 | Design language (“Civic Paper”) | `mockups/00-design-language.html` | — (tokens, rules, anti-patterns) |
| 01 | World view & HUD | `mockups/01-world-hud.html` | *What do I do next, and with what?* |
| 02 | Genos sheet | `mockups/02-genos-sheet.html` | *Why is my genos like this, and what should I fix?* |
| 03 | Household & lineage | `mockups/03-household-lineage.html` | *Can we afford this life, and who continues it?* |
| 04 | Work & career | `mockups/04-work-career.html` | *Do I stay, apply, train, or quit?* |
| 05 | Market & budget | `mockups/05-market-budget.html` | *What do we eat this week, and what does it cost later?* |
| 06 | People & conversation | `mockups/06-people-conversation.html` | *Who do I spend time with, and what do I ask of them?* |
| 07 | Interior cutaway | `mockups/07-interior-cutaway.html` | *What do I do in this room, right now?* |
| 08 | Life & legacy | `mockups/08-life-legacy.html` | *What is my life adding up to, and who inherits it?* |
| 09 | A new life (creation) | `mockups/09-new-life.html` | *How much continuity do I want to start with?* |

---

## 7.1 UX principles (ratified — supersedes the v0.1 proposal)

The five proposed principles are **kept**. One is added, because the prototype taught it the hard way.

1. **Legible depth.** The town is deeply simulated; the UI's job is to make it *readable*, never to hide it
   behind abstraction. Concretely: **every bar, badge and number carries a cause line in the simulation's own
   words** ("last meal 11 h ago", "rent lands before the second wage", "requires Endurance 5 · you: 3 ✗").
   A measure without a cause is a defect, not a simplification.
2. **Person-first.** The default view is the player's life — needs, home, money, family — with the society as
   context. Global metrics (Gini, vacancy, tick time, population) belong to the admin/observer surfaces that
   already exist; they never appear on the play surface.
3. **Desktop-native.** 1440×900 reference, minimum 1280×720, mouse + keyboard. Panels may dock, float or hide;
   they never reflow into a phone layout. UI scale is independent of world zoom.
4. **Diegetic where possible.** Home, workplace, shop and school are *places you go*. If a screen could be a
   place, it is a place: the market is a shop with a queue and closing time, not a catalogue.
5. **Quiet by default.** Emergent floods are **summarised**, not shouted: one **Town bell** digest, no toasts,
   no pop-ups over the world, no modal interrupts. Anything urgent is also somewhere the player can walk to.
6. **One accent (new).** Terracotta is reserved for *the player's own intent and their own genos*. Everything
   else is information. If a screen looks urgent, the player must be the reason.

---

## 7.2 The design language: “Civic Paper”

The prototype HUD is `rgba(12,18,24,.82)` panels in cyan-on-slate, system-ui at 13 px — one dark rectangle per
subsystem, all at equal weight, at the same temperature as the night town it sits over. It was right for
proving the model and wrong for framing a game.

**Civic Paper** replaces it with paper-and-ink chrome: a warm neutral surface that separates interface from
world by *temperature* rather than by shadow, stays legible over grass, brick and dusk, and reads like the
artefacts the simulation actually produces — ledgers, tenancy papers, school letters, wage slips. That is not
a mood; it is the design stating the game's promise (a conserved, ledgered economy where every transfer is
recorded) in the material of the interface.

### 7.2.1 Tokens

| Group | Tokens | Rule |
|---|---|---|
| Paper / ink | `#F5F1E8` paper · `#EDE7D9` recessed · `#1B2430` ink · `#7D8794` labels · `#CFC5AF` hairline | One hairline separates surfaces; a single soft shadow lifts floating ones. |
| Action | `#C4532A` accent · `#8F3A1B` accent ink | Player intent only. Never decorative, never for admin states. |
| Domains | `#B8862B` money · `#2E6E5B` body/needs · `#2F5D8A` work · `#A63A5A` social · `#5B4B8A` legacy | Semantic. Each colour always travels with its word (“work”, “social”). |
| Type | display serif (names, places, titles) · UI sans (interface) · mono, tabular figures (every quantity) | A balance that reflows when it changes is a balance the player cannot trust. |
| Space | 8 px unit; 4/8/12/16/24/32 only; radii 2–4 px | Paper has no pills. Roundness is reserved for *lives* — portraits, genos dots, the intent dial. |
| Motion | 120 ms hover · 180 ms panel · 240 ms mode change · 0 under `prefers-reduced-motion` | The camera moves; the chrome does not. No bounce, spring or parallax. |

Full sheet, component kit and do/don't list: `mockups/00-design-language.html`.

### 7.2.2 The five interaction models

These are the parts of the design that are *new* rather than restyled. Each one exists to solve a problem the
prototype had.

**(a) The intent dial — the UI *is* the contract.**
Right-clicking the player's genos opens a radial dial of verbs costing nothing to browse, each showing its
cost, duration and availability *before* commitment ("Eat something · 35 min · ₡ 6.50 · pantry"). The dial is
grouped by where the genos is, so the verbs that appear are the verbs the server will accept there. Crucially,
**each dial entry is a typed intent** — the dial is not a menu of client behaviours, it is a rendering of the
player's action set in `Genelife.Contracts`. Adding a verb means adding an intent, not a button.

| Dial verb | Intent (contract surface) | Preconditions shown |
|---|---|---|
| Eat something | consumption / eat from stock or pantry | pantry stock, money, time |
| Go to work | movement → work-shift intent | commute time, shift start |
| Talk to Wren | social interaction intent | proximity, friendship value |
| Buy groceries | enter building → purchase intent | inside a store, money, basket |
| Go home | movement intent | distance, door access |
| Apply for a job… | employment application intent | openings, skill gates |

**(b) The soma ring — physiology at a glance, never four bars forever.**
A quartered ring around the portrait shows food, rest, hygiene and resolve in one shape; the sheet opens the
detail. Needs only *surface* as chips when they cross a threshold (“hungry”), which is the structural version of
"quiet by default".

**(c) The day ribbon — the answer to “what will happen today?”.**
A horizontal band across the bottom of the world view: sleep, errand, shift, commute, a visit, household time,
with the current moment marked and the next free hour stated. It is both a *forecast* (drawn from the schedule
the simulation will run) and the editor for that forecast.

**(d) The Town bell — one digest, no flood.**
A single panel with a count, grouping events by consequence rather than by subsystem: rent shortfall (with the
shortfall number), a school place, a completed shift's pay, a knock at the door. Nothing else ever appears over
the world.

**(e) The dollhouse interior — a roof that lifts.**
Entering a building keeps the town on screen as a dark surround and lifts the roof off a floor plan, with
affordance tags on objects (“Table — eat here · 20 min”; “Kitchen — cook · needs flour”). Unavailable
affordances explain themselves instead of greying out silently. Interiors are player-scoped by design (per
chapter 03 §3.4), and the UI never asks for more than the one building's plan.

### 7.2.3 Refusal is a reason, not a boolean

The dial and the dollhouse both promise to **explain, never grey out** — which means a refusal has to
arrive as a *reason the interface can render*, not as `allowed: false`. The simulation returns far more
distinct codes than a player can meaningfully act on, so the design owns a **closed set of reasons** and
maps the wire vocabulary onto it. One source: `GDD/mockups/assets/refusal-reasons.json` (reasons, the
canonical code strings, their surface and producer), guarded by `RefusalMappingTests`.

| Reason | The player is told | What it offers |
|---|---|---|
| `no_money` | "You can't afford that yet" — with the shortfall in figures | — |
| `no_stock` | "There is none left" | Buy groceries |
| `not_here` | "You need to be there" | Go there · *n* min |
| `not_open` | "That isn't open right now" | — |
| `already_done` | "You have already done that" | — |
| `not_yours` | "That isn't yours to do" | — |
| `expired` | "That offer has lapsed" | Apply again |
| `busy` | "There is a queue" | Wait |
| `declined` | "They said no" | — |
| `not_ready` | "That can't happen right now" | — |

Four rules make this a design language rather than a lookup table:

1. **The code selects the presentation; the data supplies the numbers.** "You're ₡ 12.40 short" is
   arithmetic on `costCents` and the balance, never a server string — the same rule as cause lines (§7.4).
2. **An unknown code is a version-skew signal, not a prompt to improvise.** With the mapping guarded in
   both directions, a matching build cannot emit an unmapped code; if one arrives the client is behind, and
   it must say so and re-request rather than invent a sentence. The catalogue envelope therefore carries
   its shape and refusal-set version next to `asOfTick` (chapter 08 §8.5(1)).
3. **Canonical strings are frozen, not just enum values.** The shipped browser client keys its message and
   its notification cue off the string, so a rename that preserves a byte value still breaks the player's
   experience. Renames move the mapping in the same change, or the guard fails.
4. **The mapping is consumer-side and per-surface.** It is keyed by the *wire string*, so codes produced by
   the movement engine or the social surfaces are covered without being converted — each entry declares
   `surface` (`dial` / `social` / `engine` / `system`) and the producer that emits it, and only the dial's
   entries are required to be reachable from the action path.

---

## 7.3 What each surface must

| Surface | Not this |
|---|---|
| World HUD | A dark overlay with one panel per subsystem; a debug overlay as the default view. |
| Genos sheet | A stat block of raw numbers; a tooltip-only explanation. |
| Household | A finance dashboard. It is a household *document*: who lives here, what the week costs, who inherits. |
| Work | A job list with an Apply button that pretends to guarantee an outcome. |
| Market | A price table with a nutrition scoreboard. |
| People | A separate chat app; a friend list that ignores residents. |
| Interior | A building information panel with a "use" button. |
| Life & legacy | A game-over dialog at the moment of death. |
| Creation | A face editor the simulation then ignores. |

---

## 7.4 Interface → simulation mapping — **closed** (answered in §8.5)

The seven items below were raised here as contract questions. **All seven are now answered in chapter 08 §8.5**,
each with a state. This section is kept — rather than deleted — because the mapping *is* the design's contract:
it records what the UI is entitled to expect from the server, and it is the checklist a client implementer reads
before writing a screen.

| # | Design need | §8.5 answer | State | What it means for the UI |
|---|---|---|---|---|
| 1 | **Action catalogue** (valid verbs per context, with cost / duration / preconditions / refusal code) | Server-provided read model, produced by the same pure `Evaluate(worldState, genos, kind, payload)` the tick drain calls; entries carry `asOfTick` | **Decided** | The dial renders the catalogue verbatim. It may *present* a `refusalCode` as a sentence, never invent the rule. Stale `asOfTick` ⇒ re-request, not guess. |
| 2 | **Cause lines** ("last meal 11 h ago") | Structured coded facts (`NutritionState.LastMealTick` already persisted); client owns phrasing, server prose banned | **Decided** | Every cause line in these mockups is `(code, numeric fact, sinceTick)` rendered against the sim clock. No server strings, no invented explanation. |
| 3 | **Scoped interior request** (dollhouse) | Scope-3 map-snapshot pattern, `specHash`-invalidated, cached on entry; **no interior field on the building entity**; player position inside replicates as per-genos `InteriorLocation` (and must be in `GenosesEqual`) | **Decided, not built** | The interior screen is a *request*, never a property: entry → request plan → render → cache; exit → drop. Room occupancy for the player only. |
| 4 | **Day ribbon**: planned vs actual | **Committed + scheduled + recorded.** No prediction. | **Decided** | Three block states, visually distinct: committed (solid), scheduled (hatched), recorded (filled + tick). Never a fourth "probable" state. |
| 5 | **Fonts + UI scale** | Two bundled fonts; HUD scale independent of camera zoom | **Requirement (client proves it)** | No longer a design question — it moved to the client repo. See §7.6 for the check that proves it. |
| 6 | **Digest grouping key** | `(eventKind, subjectEntityId)` over a rolling window, server-computed, no new persistence | **Decided** | The Town bell is one list per subject: four rent lines become *one* row with a count. The UI keeps the wording and the severity ordering. |
| 7 | **Presence vs residents** | Already in the contract: `IsPlayerControlled` is replicated and in `GenosesEqual`; residents carry `GenosStatus.Name`; players add `PlayerPresenceDto` | **Confirmed** | The player/resident chip is a read of `IsPlayerControlled`. Nothing in the design language has to move — the chip was already drawn that way. |

**Two items changed hands, and neither moves the design:** fonts/scale is now an obligation on the Godot client
(§7.6), and the presence chip is confirmed as existing data rather than a request. **Nothing in the design
language had to change to accommodate the answers** — which is the evidence that the mockups were drawn against
the contract and not against an imagined one.

---

## 7.5 Accessibility and input (binding on the client)

### 7.5.1 Contrast, measured

The token set was **verified, not asserted** — and the first pass failed its own rule: labels came out at
`3.23:1` and money at `2.87:1` on paper. The palette was darkened until the rule was true rather than the rule
softened to fit the palette (WCAG 2.1 relative luminance):

| Pair | Before | Final | Verdict |
|---|---|---|---|
| Ink `#1B2430` on paper | 13.9:1 | `#1B2430` — 13.9:1 | AAA |
| Ink 2 (secondary) on paper | 6.6:1 | `#3F4A57` — **8.0:1** | AAA |
| Ink 3 (labels) on paper | 3.2:1 ✗ | `#586271` — **5.5:1** | AA |
| Money on paper | 2.9:1 ✗ | `#8A5A12` — **5.3:1** | AA |
| Accent on paper | 4.0:1 ✗ | `#A94520` — **5.2:1** | AA |
| Body / work / social / legacy on paper | 5.3 / 6.1 / 5.5 / 6.6 | unchanged | AA |
| Paper on ink (primary button) | 13.9:1 | unchanged | AAA |

Consequence for the client: these are the **only** permitted text colours on paper. A new colour is added by
measuring it, not by picking it.

### 7.5.2 Rules

- Body text ≥ 7:1 on paper; labels and secondary text ≥ 4.5:1. No grey-on-grey for anything the player must read.
- Domain colour is never the only signal; every chip carries its word, every need carries a value and a direction.
- Targets ≥ 32 px (mouse); ≥ 44 px on any touch-capable build. Dial items are 132×34 with 8 px gaps.
- Visible focus: 2 px ink outline, 2 px offset, on every focusable element, including the world canvas.
- Keyboard map: `WASD`/arrows move · `RMB` intend · `E` interact · `Tab` household · `M` map · `B` bell ·
  `Space` pause · `Esc` back. **Every dial verb has a chord**, so the game is fully playable without a mouse.
- UI scale 100/125/150 % independent of world zoom; the ribbon reflows, panels dock.
- The journal export is plain text: the record remains readable if the chrome is lost.

---

## 7.6 Client acceptance checks

Five of the design's requirements cannot be verified in a document, only in the client. Each is written here as
a **check with an observable result**, so "the UI follows the GDD" is testable rather than a matter of opinion.
They belong in the desktop client's own test suite (screenshot + assertion), not in this document.

| # | Check | Passing looks like |
|---|---|---|
| AC-1 | **Fonts bundled.** The client ships its own serif (display) and mono (figures); no system-font fallback is load-bearing. | A screenshot with the fallback font disabled still renders the display serif and tabular figures; `1 284.40` and `1 188.40` occupy identical widths. |
| AC-2 | **HUD scale independent of camera zoom.** 100 / 125 / 150 %. | At world zoom 0.5× and 2×, HUD text height is unchanged for a fixed UI scale; at 150 % no panel clips its content and the day ribbon reflows. |
| AC-3 | **Frame contract.** 1440×900 reference, 1280×720 minimum. | At 1280×720 every surface still shows its primary decision and its primary action without scrolling; nothing overlaps the ribbon. |
| AC-4a | **Catalogue ⇄ rule parity (server, no client needed).** The catalogue never offers a verb `Evaluate(...)` would refuse, and never withholds one it would accept — with the **reason**, not just a boolean (the design's side of that contract is §7.2.3 and `refusal-reasons.json`). | Over an enumerated context matrix (every `PlayerActionKind` × fixture states: no money, no stock, wrong building, wrong tile, cooldown active, valid) the catalogue entry equals the `Evaluate` result **including `refusalCode`**. A boolean-only assertion would pass while the dial's refusal line and the dollhouse's “explain, don't grey out” promise stayed unmet. Lands with Phase D, before any Godot pixel. |
| AC-4b | **Dial ⇄ catalogue parity (client).** The dial renders the catalogue faithfully. | The rendered dial set equals the response; a forced stale `asOfTick` shows the re-request state, not a guessed list. Asserted against a shape AC-4a has already frozen. |
| AC-5 | **Cause lines contain no server prose.** | Every explanation string visible on the genos sheet is composed client-side from `(code, fact, sinceTick)`; a string arriving from the server is a test failure. |
| AC-6 | **Contrast.** §7.5.1 ratios hold in the shipped theme. | **Enforced, not aspirational.** `ContrastTokenTests` (`Genelife.Domain.Tests/Docs`) reads the colours out of `mockups.css` and the rule out of `contrast-pairs.json` — one copy each — and asserts 15 text/surface pairs; it carries `Category=Documentation`, so it runs inside the **required `unit` check** and a palette regression fails the merge. Its `The_check_can_fail` test is a permanent negative control (the v0.2 label colour, 3.23:1). `python3 GDD/mockups/assets/contrast_check.py` is the designer's fast local loop over the same rule. |

Until those exist, the mockups are the *intent* and the checks are the *proof*. I would rather the first client
build fail AC-4b loudly than ship a dial that quietly disagrees with the server.

---

## 7.7 Anti-patterns (binding — from the prototype's lessons)

1. **No web-HUD port.** No dark translucent rectangles with a cyan accent, no panel-per-subsystem, no debug
   overlay as the default view.
2. **No admin dashboard in play.** Gini, vacancy, tick time and population are observer surfaces.
3. **No number tables as a substitute for a place.** If it can be a shelf, a letter or a room, it is not a table.
4. **No notification floods.** Nothing over the world except the bell's count.
5. **No unexplained state.** A bar without a cause, a disabled control without a reason, or a cost revealed
   after commitment are all defects.
6. **No decorative colour.** A rose header that means nothing is a lie about the social domain.
7. **No client-side rule.** If the UI decides whether an action is possible, it has become a second simulation.

---

## 7.8 Open items

Nothing in this chapter is open against the team. What remains is **product-level**, and it is parked in chapter
10 §10.2 rather than here, so the bible has one place for pending rulings:

- **No face editor** — appearance derived from traits / age / sex (mockup M09).
- **Death continues the household** as an heir (mockup M08).

Both follow from the brief and from §5.5; neither is styling, and both change the feel of the game, so they want
an explicit PO ruling rather than a designer's assumption.

---

*Revision: v0.4 — 2026-11-28, @adriano. Added §7.2.3 (refusal is a reason, not a boolean): the closed
reason set, the four rules, and `refusal-reasons.json` as the one source, guarded by `RefusalMappingTests`
in the required unit check.*

*Revision: v0.3 — 2026-11-28, @adriano. §7.4 closed against chapter 08 §8.5 (all seven items answered, two
changed hands); §7.5 contrast re-measured and the palette darkened where it failed its own rule; §7.6 client
acceptance checks added; open items reduced to the two PO rulings parked in chapter 10.*

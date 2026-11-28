# 07 — UI/UX Design Language & Screens

> **Status: Draft v0.2 — authored by @adriano (UI/UX).** Supersedes the outline in v0.1.
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

---

## 7.3 What each surface must *not* be

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

## 7.4 Interface → simulation mapping (for @tech-leader)

The design assumes the following of the wire contract and the client boundary; each is a question worth
answering in chapter 08 rather than a decision I should take alone.

1. **An action catalogue.** The dial needs, per context, the list of currently valid verbs with their costs,
   durations, preconditions and refusal reasons. Is that a contract read model (e.g. "available actions for
   genos X at position P"), or does the client derive it from raw state? **Recommendation: server-provided** —
   the client must not become a second rule engine (V3's invariant).
2. **Cause lines.** "Why" strings ("last meal 11 h ago", "rent lands before the second wage") are cheap to
   render but must not be *invented* by the client. Either the server sends structured causes alongside each
   value, or the client is limited to facts it already stores (timestamps, ledger entries). **Recommendation:**
   structured, minimal cause payloads in the existing read models; no free-text from the server.
3. **Scoped interior request.** The dollhouse needs one building's interior plan on entry and a cache until
   exit — the map-snapshot pattern already used for the world (chapter 03 §3.4). No interior field on the
   building entity on the wire.
4. **Time and schedule.** The day ribbon draws the schedule the server will execute. It may show *planned*
   blocks and *actual* blocks differently; it must not predict outcomes (e.g. "you will be fired") that the
   server has not decided.
5. **Text rendering in Godot.** Serif display + mono figures are a hard requirement of the language. Godot's
   default font stack does not ship a serif; the client must bundle two fonts and support the UI scale setting
   (100/125/150 %) without re-laying out the world.
6. **Notification digest.** One digest requires a server-side grouping rule (or a client rule over a small
   event feed). I have mocked the *presentation*; the grouping key is a technical decision.
7. **Presence vs residents.** "Players and residents in one list, marked by a chip" requires presence data that
   already exists (§5.6) plus a stable display name for residents; confirm the contract carries both, and that
   *who is a player* is not inferred client-side.

---

## 7.5 Accessibility and input (binding on the client)

- Body text ≥ 7:1 contrast on paper; labels ≥ 4.5:1. No grey-on-grey for anything the player must read.
- Domain colour is never the only signal; every chip carries its word, every need carries a value and a
  direction.
- Targets ≥ 32 px (mouse); ≥ 44 px on any touch-capable build. Dial items are 132×34 with 8 px gaps.
- Visible focus: 2 px ink outline, 2 px offset, on every focusable element, including the world canvas.
- Keyboard map: `WASD`/arrows move · `RMB` intend · `E` interact · `Tab` household · `M` map · `B` bell ·
  `Space` pause · `Esc` back. **Every dial verb has a chord**, so the game is fully playable without a mouse.
- UI scale 100/125/150 % independent of world zoom; the ribbon reflows, panels dock.
- The journal export is plain text: the record remains readable if the chrome is lost.

---

## 7.6 Anti-patterns (binding — from the prototype's lessons)

1. **No web-HUD port.** No dark translucent rectangles with a cyan accent, no panel-per-subsystem, no debug
   overlay as the default view.
2. **No admin dashboard in play.** Gini, vacancy, tick time and population are observer surfaces.
3. **No number tables as a substitute for a place.** If it can be a shelf, a letter or a room, it is not a table.
4. **No notification floods.** Nothing over the world except the bell's count.
5. **No unexplained state.** A bar without a cause, a disabled control without a reason, or a cost revealed
   after commitment are all defects.
6. **No decorative colour.** A rose header that means nothing is a lie about the social domain.

---

## 7.7 Open questions for the team

- **@tech-leader** — items 1–3 and 6–7 in §7.4 are contract questions; the dial and the dollhouse cannot be
  built as designed without them.
- **@roberto** — the surface inventory in §7.3 now has a *decision* per surface; if you want, fold that column
  into chapter 11's vignettes so the same moment is described once in fiction and once in interface.
- **Product owner** — two design positions that change the feel and are worth an explicit ruling:
  (i) **creation has no face editor** (appearance derived from traits/age/sex, mockup M09) and (ii) **death
  continues the household** rather than restarting it (mockup M08). Both follow from the brief and from §5.5,
  but they are product-level positions, not styling.

---

*Revision: v0.2 — 2026-11-28, @adriano. Design language ratified, six principles, nine mockups delivered under
`GDD/mockups/`, interface→simulation mapping added. Supersedes the v0.1 outline.*
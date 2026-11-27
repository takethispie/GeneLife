# Building interiors — model, contract, UX, and which client owns them

**Author:** @roberto (Solution Researcher) · **Date:** 2026-11-27 · **Branches:** `docs/roberto/building-interiors` (dossier) · `docs/roberto/interiors-po-rulings` (rulings)
**Verified against:** `master` @ `dc5f2ce` · **Card:** *"research(interiors): how to enter and display building interiors, and which client owns them"* (`t_68262a29`)
**PO rulings recorded:** 2026-11-27 (all five open questions answered — see [08](08-open-questions-for-the-po.md);
re-framing applied in [07](07-recommendation.md))

**Companion documents:** this dossier extends the house style set by
[`../post-perf-feature-evolution.md`](../post-perf-feature-evolution.md) (the deep-dossier format: measured premise,
lettered parts, "what does not transfer", verdict, sources). That document records the product-owner ruling of
2026-11-27 that performance work is out of scope until the **Godot desktop client reaches parity with the browser
client** — so Godot is a *declared direction*, not a hypothetical, and that ruling is a load-bearing input to the
sequencing question below.

---

## The question

From the product owner, verbatim:

> there is a big research to do on a future subject: how to handle entering and displaying building
> interior, should this research be deferred to after making the godot desktop client or is the web game
> client not far enough and so this would still be in the scope that web client?

Two questions are tangled in that sentence, and the whole dossier is organised to separate them:

1. **Does the research get deferred?** (When do we decide the model?)
2. **Which client implements interiors first?** (Who draws them?)

## Verdict, in one paragraph

**Do not defer the research, and implement the first renderer in the browser.** The space model, the data, the room
scope of the snapshot and the entry intent are *client-agnostic* — they live server-side and in `Genelife.Contracts`,
not in PixiJS or Godot — so the decision can and should be made now, and the part that is not client-specific (the
model and the contract) is the part that is expensive to change later; only the *renderer* is disposable, and it is
the cheap half (the browser already ships the entry flow with nothing to show on arrival,
`clients/browser/src/input/building-selection.ts`, and it is the only client that exists in the tree). **The PO's
five rulings of 2026-11-27 leave this conclusion intact and sharpen its framing.** The decisive one is **Q4**: the
browser client is a **stepping stone**, not a long-term ship, so the browser renderer is a **prototype-only slice**
whose purpose is to validate the model and the UX and whose **sprite code is expected to be retired** when the
**real renderer lands in Godot** (procedural, spritesheets/tilesets, per Q2); the **"do not defer the research"** and
**"decide the model and the contract now, server-first"** halves of the recommendation **survive unchanged**,
because the durable artifact is the model and the contract and neither depends on which client renders first. The
remaining rulings scope v1: **player-only interiors** (Q1, NPCs later and performance-gated), **private interiors**
with shared as a v2 (Q5), and a **roof cutaway** as v1 with the multilevel cutaway as v2 (Q3) — where Q3 carries a
consequence that becomes a **named planning item** (footprints sized to hold real interiors, and a bigger world —
see [07](07-recommendation.md#named-planning-item--footprint-sizing-and-world-size-q3-consequence)). What is thrown
away if Godot replaces the browser is the Pixi room-drawing code and nothing else (see
[06](06-client-implications.md) and [07](07-recommendation.md)).

**One-line conclusion:** *the model is the deliverable, the renderer is disposable.*

## File index

| File | Answers |
|---|---|
| [01-problem-and-current-state.md](01-problem-and-current-state.md) | What "interior" means here; what exists today, with `file:line` |
| [02-prior-art.md](02-prior-art.md) | How shipping games/sims do interiors, and one that deliberately does not |
| [03-architecture-options.md](03-architecture-options.md) | The real options, with a comparison table (cost / risk / what each forecloses) |
| [04-data-and-sim-model.md](04-data-and-sim-model.md) | The space/room model, authored vs procedural, authority for "inside" |
| [05-contract-and-server.md](05-contract-and-server.md) | What the server owns and sends; snapshot cost at scale; scope vs payload |
| [06-client-implications.md](06-client-implications.md) | Web/Pixi vs Godot side by side; what is shared and what is throwaway |
| [07-recommendation.md](07-recommendation.md) | The firm recommendation, the reasoning, the risks, the falsifiers, the post-ruling update, and Q3's planning item |
| [08-open-questions-for-the-po.md](08-open-questions-for-the-po.md) | The product decisions, **now all answered** — each with its ruling and what it commits the project to |

## Method note

Every claim about *this* codebase carries a `file:line` against `master` @ `dc5f2ce`. Every claim about prior art
carries a numbered source in the relevant file. Where a claim is an inference rather than a fact, it is marked as
such. This card produced **documentation only** — no `.cs` file and no `clients/browser/src` file was changed
(acceptance criterion 5).

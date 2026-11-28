# 04 — Simulation Core

> The Dwarf-Fortress half: the systems that make the town live on its own. This chapter
> describes the **behaviour and intent** of the systems — the "why" and "what", not the
> implementation. Implementation invariants live in chapter 08.

## 4.1 Time

- The simulation advances in **ticks**; **1,200 ticks = 1 in-game year**, giving a fine
  enough grain for a day's routine at a coarse enough grain to run decades.
- Systems run on **cadences** (every tick, every month, every pay-cycle); the calendar drives
  rent, payroll, taxes, pensions and consumption. The town's clock never stops for a player.

## 4.2 Needs — the personal rhythm

Every genos has needs — **Hunger, Energy, Hygiene, Bladder** (the eat–shower–sleep cycle).
Needs **decay** over time and are **topped up** by *actions* (eating, sleeping, showering,
using the toilet). A need sawtooths between a **top-up threshold** and full, so the *threshold*
sets the population's average satisfaction — the single most important dial for the "feel" of
a busy but not frantic town.

Design intent:
- Needs are the **personal heartbeat** that makes a genos feel like a person, and the reason
  a "restaurant", "home" and "shower" are *places with meaning*.
- Needs must be **legible** to the player for their own genos (a Sims-grade need read-out).

## 4.3 Decision → Action — how genoses choose

- Each genos picks, from its due needs and its obligations (shift, school, the shop), the
  **next action**; an in-flight need action must be allowed to complete, and no second system
  may overwrite a running action (work schedules defer to in-flight needs). This ordering is
  an **invariant**, not a nicety: violating it silently disables need restoration.
- The decision layer is the seam where the town's **emergence** happens: thousands of small
  choices produce commuting peaks, shopping trips, idle hours and service pressure.

## 4.4 Economy — a closed, conserved loop

- **Money is integer cents**, and **every transfer is ledgered** — nothing is created or
  destroyed implicitly. This conservation is what lets the simulation be *trusted* over
  decades and is a core reason the society reads as real.
- **Companies** post jobs, hire, pay wages, earn revenue, pay tax, fund pensions and can fail.
  Offices are the workplaces; schools the training ground.
- **Households** earn, pay rent, buy essentials (food, hygiene) and discretionary goods, and
  can be pushed into arrears, eviction and homelessness.
- **Utilities** (water and electricity) are modelled as **networks** with outages and
  household billing; a **grocery catalog** supplies stock, pricing and nutrition.
- The economy is **closed**: a firm sector cannot pay out more in wages than it receives in
  revenue, and consumer demand is recycled back to firms. Balance bands (rent burden,
  vacancy, Gini, unemployment, wage share) are **reported** as health signals — but retuning
  them is explicitly **not** current work (standing product ruling).

## 4.5 Lifecycle — birth to estate

Every genos runs a **full life**: childhood and education → working life → retirement →
elderly → death. The lifecycle spans:
- **Schools/skills** shaping a genos' employability and career.
- **Work, attendance, careers, unemployment, pensions** over a whole working life.
- **Health and mortality**, and **estates** that pass on at death.
- **Immigration** as a population inflow, alongside **births** (§4.6) as a continuity path.
- **Player life** is the same lifecycle, player-driven: employment, housing, consumption,
  family actions, aging, death and **heir/respawn continuation**.

## 4.6 Family, genetics and reproduction (V4)

- **Traits** are a compact record struct with byte-backed enums; they are **inherited and
  mutated**, so lineages drift over generations.
- **Relationships** are directed edges — `Parent`, `Child`, `Partner`, `ExPartner`,
  `Guardian`, `Ward` — forming a **family graph** the player can observe.
- **Attraction** is an integer basis-point score, so pairing is explainable, not a black box.
- **Reproduction** produces children who grow in the same simulated economy: guardian-paid
  childhood consumption, childhood gates, widowhood and guardianship, and **relationship-
  based inheritance** with minor trusts and housing transfer.
- **Births supplement immigration** as the way the town continues itself.

## 4.7 Appearance (V5)

A genos' **appearance is derived** from traits, age and sex (skin tone, face shape, layered
parts) — so a child resembles its parents *by system*, not by hand-authoring. Appearance is
the visual bridge between the genetics pillar and the player's emotional attachment to a
geno.

## 4.8 What the simulation must never lose

The product owner has said the **current depth is "very well within what I want."** The
simulation core is therefore the pillar to **protect**: new features must extend it or sit on
top of it, never simplify it, and never move its authority into a client.

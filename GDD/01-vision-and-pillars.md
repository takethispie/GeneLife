# 01 — Vision & Pillars

## 1.1 The one-sentence vision

> **GeneLife is a persistent, 2D, open-world life simulation in which a deep autonomous
> society of genoses lives alongside you, and you steer your own genos through a life you
> make — work, home, family and money — inside a town shared with other players.**

## 1.2 Design lineage (the product owner's brief)

The product owner framed the target as a **mix of three games**:

- **GTA** — a big, open, freely traversable space, with a lot of *freedom* about where to go
  and what to do. *Transposed:* GeneLife is 2D and top-down, but the world is large, open and
  explorable, and it is a *sandbox* rather than a corridor of scripted missions.
- **Dwarf Fortress** — *heavy simulation* as the engine of the world. *Transposed:* the
  population, economy, housing, lifecycle and family systems are already the core of
  GeneLife and the product owner has stated the current simulation depth is *"very well
  within what I want."* Depth is a pillar to **protect and extend**, not to trade away.
- **The Sims** — *control over your own character(s)*, domestic life, managing a person's
  needs and household. *Transposed:* the player directly directs **their own genos** — a
  personal-life control layer on top of the society simulation.

The result, in the product owner's words, is a ***"make your own life"* type of simulation.**

## 1.3 The four pillars

These are the load-bearing pillars of the game's identity. A feature that weakens one of
them without strengthening another must be rejected; a feature that strengthens none of
them is out of scope.

### Pillar 1 — A Living World (simulation depth)
The town is **alive without the player**. Genoses are born, schooled, employed, paid,
housed, taxed, pensioned, made redundant, made homeless, partnered, bereaved and buried by
systems that run on their own. The player observes and participates in a genuine *society*,
not a diorama. This pillar is GeneLife's differentiator and its most mature asset.

### Pillar 2 — An Open 2D Sandbox (spatial freedom)
The world is an **explorable tile map** with buildings you can walk to, enter, and use.
Freedom is spatial and systemic: go where you want, choose a job, choose a home, spend or
save. There is no forced critical path. The world is large enough that a life can be spent
in a corner of it. *("Big open space of GTA, but in 2D.")*

### Pillar 3 — Your Geno, Your Life (personal control)
The player **controls their own genos** the way The Sims controls a household: direct their
actions, manage needs, hold a job, run a home, build a family, make money and spend it. The
player's life is *legible and personal* even while the society around it is emergent.

### Pillar 4 — A Shared Persistent Town (multiplayer)
The town is **server-authoritative and shared**: many players inhabit the same simulation
at once, see each other, and interact socially. The world does not pause for one player, and
no client is ever the source of truth.

## 1.4 The target experience (player fantasy)

- *"I am living a life, not playing a level."* There is no win screen; there is a biography.
- *"The town keeps going whether I log in or not."* Persistence and consequence.
- *"My choices are real because the world is real."* If I quit my job, my rent is still due;
  if I have a child, the child grows up in the same simulated economy as everyone else's.
- *"It feels like a place."* A town with neighbourhoods, workplaces, shops, schools and
  houses — not a menu of abstract systems.

## 1.5 Anti-goals (what GeneLife is *not*)

Stated explicitly so the identity is defended against drift:

1. **Not a combat game.** There is no shooter/combat spine. Conflict, where it exists, is
   social and economic, not ballistic.
2. **Not an authored narrative.** No linear main quest. Stories emerge from systems.
3. **Not a spreadsheet.** Dwarf-Fortress depth must be *legible* to the player through the
   UI; opacity is a failure of the UI pillar, not a virtue of the simulation.
4. **Not single-player-only.** The shared-world pillar is core; features are designed
   server-first with multiplayer in mind even when the first slice is solo.
5. **Not a reskin of the browser prototype.** The current browser client is a **stepping
   stone** whose renderer is expected to be retired; the durable artifacts are the **model,
   the contract and the UX**, and the real renderer is the desktop client. Copying the
   prototype's layouts into the GDD is explicitly *out*.

## 1.6 The client transition (context for every chapter)

The project is **bridging from the PixiJS browser client to a desktop client**. Decided
positions this GDD must hold:

- The desktop client is **Godot, written in C#** (not GDScript) — it reuses the C# simulation
  and shares the **wire contract** (`Genelife.Contracts` / `Genelife.Messages`), never the
  domain/application assemblies (no client-authoritative simulation).
- Desktop-only; the web target stays the browser client. C# cannot export to web in Godot 4.
- The browser client is a **prototype-only slice** to validate the model and UX.
- The **model + contract + this GDD are client-agnostic and durable.**

This is the "bridge" chapter 02 quantifies.

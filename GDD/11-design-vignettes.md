# 11 — Design Vignettes (what the game plays like)

> Design vignettes make the pillars concrete. They are **illustrative**, not scripted content
> — they describe experiences the systems should be capable of generating, and they are a test
> the roadmap can be held against ("can the build do this yet?").

## 11.1 A day in a life (the personal scale)
You wake in your rented house. Your genos' **Energy** is low, **Hunger** is climbing. You walk
to the kitchen, eat; you shower; you leave for your shift at the office across town. On the
way you pass a neighbour's genos commuting the other way; the shop is busy because it is a
month's start and households are buying essentials. You work the shift, get paid, and pay
rent before the day's end. The rent is due whether or not you remembered. Nothing here is
scripted — it is four systems (needs, economy, lifecycle, movement) agreeing about a Tuesday.

*Tests:* do needs, commuting, the shop and rent read as *one coherent day* to the player?

## 11.2 A life across generations (the society scale)
You grow old, retire, and die. Your genos' child — with traits inherited and mutated from you
and your partner — inherits your genos' estate and, if you choose, you **continue as that
heir**. Meanwhile the town has aged around you: some companies failed and new ones hired,
some houses emptied and filled, the population drifted. You are not restarting; you are living
a *lineage* inside a town that never paused.

*Tests:* does inheritance+heir continuation feel like continuity, not respawn?

## 11.3 The bridge demo (the client transition)
A new player launches the **desktop** client. They pick a home, get a job, walk the open tile
world, enter their house through a **cutaway interior**, manage needs and money on a
**desktop-native HUD**, and see other players moving in the shared town. Every fact on screen
came from the **wire contract** — the desktop client holds no game rules of its own.

*Tests:* is the desktop client a *renderer of the model*, not a second simulation?

## 11.4 The Dwarf-Fortress moment (emergent depth)
You watch the town's metrics after a decade with no player input: firms have opened and shut,
a family line has grown across three generations, some households slipped into arrears and
recovered. Nothing was authored; all of it was *simulated*. This is the pillar the product
owner called **"very well within what I want"** — the thing the GDD exists to protect.

*Tests:* can you leave the town running and be *surprised* by a plausible story?

## 11.5 The open-space moment (freedom)
The player ignores every objective (there are none) and walks the world: to the edge of the
generated town, into a school, out to a venue, home again. Freedom is not a mission list —
it is a place big enough and real enough to wander, where wandering still has consequences.

*Tests:* is the world worth exploring even when the player is "doing nothing"?

---

## 11.6 How to use these vignettes
Each vignette is a **playable acceptance sketch**. When a roadmap phase lands, ask: which
vignette can now be played end-to-end in the desktop client? The answer is a plainer status
report than any metric table.

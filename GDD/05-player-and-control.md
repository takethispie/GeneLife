# 05 — The Player & Control Model

> The Sims half: how a *player* inhabits the simulated town and directs a life inside it.
> This chapter defines **what a player can do** — the control model every UI mockup must
> serve.

## 5.1 Who the player is

- The player has a **genos of their own** — their character — embedded in the same society as
  every NPC. The player's genos has needs, a job, a home, money, a family and, eventually, a
  death.
- The player controls **their own genos (and their household)**, not the world. This is the
  boundary that keeps the game *personal* without breaking *server authority*: the player
  issues **intentions**, the server decides outcomes.

## 5.2 The control loop

1. **The player intends** an action (go here, take this job, buy this, eat, sleep, propose,
   have a child…).
2. The intention becomes a **typed intent**, drained **at tick start under the write lock**.
3. The **server resolves** it against the world's rules and current state; the client is
   never the source of truth.
4. The outcome replicates back via the **contract**; the client renders it.

Everything the player can do is thus an **intent in the contract**. New player verbs are added
to the contract, not improvised in the client.

## 5.3 The player's action set (life domains)

The player's life is expressed through the same domains the simulation already models:

| Domain | Example player intents |
|---|---|
| **Employment** | apply for a job, accept an offer, quit, work a shift |
| **Housing** | rent/lease a home, move, pay rent, deal with arrears/eviction |
| **Consumption** | buy groceries/essentials, manage utilities, budget |
| **Personal needs** | eat, sleep, shower, use the toilet (the needs cycle) |
| **Family** | court/partner, marry/cohabit, have children, care for a ward |
| **Social** | chat, meet, befriend, block/report, partner another player |
| **Movement** | travel the town, enter and use buildings |
| **Life-course** | age, retire, die and **continue as an heir** |

## 5.4 Why this reads as "The Sims"

The Sims' magic is **legibility + consequence at the personal scale**. GeneLife achieves it
by giving the player:
- **Direct action selection** over their own genos (not indirect policy).
- **Readable state**: needs, money, home, job, family — visible and manageable.
- **Domestic consequence**: the rent is due whether or not the player is enjoying the town.
- **A household**: more than one genos can be under player care, as a family unit.

The differentiator over The Sims is the **world**: the player's household lives inside a
*society with hundreds-to-thousands of other lives*, all simulated, all consequential.

## 5.5 Household and continuity

- The player manages a **household**, not just an avatar: home, family and shared money are
  part of the life being lived.
- **Death is not the end** of a playthrough — the player **continues as an heir**, so a
  lineage can be played across generations. This ties the personal pillar (Pillar 3) directly
  to the genetics pillar (Pillar 1).

## 5.6 Multiplayer boundary

- The player's **genos and household** are theirs; the **town and its society** are shared.
- Other players are visible and social (presence, chat, friendships, partnerships), but they
  do not control each other's genoses. Cooperation and friction are social, not adversarial.
- This boundary is what makes the shared world (Pillar 4) compatible with personal control
  (Pillar 3): *one shared simulation, many private lives.*

## 5.7 What control must feel like

- **Responsive and legible:** the player always knows what their genos is doing and why.
- **Consequential:** choices have costs that persist (a missed rent, a lost job).
- **Personal:** the player's life is *theirs* inside a town that is not.

> **UI/UX input needed (→ @adriano):** the desktop-client mockups must express this action
> set and the household/continuity model — a need read-out, an action/interaction model, a
> household view, a life/legacy view — **not** a copy of the current web HUD. See chapter 07.

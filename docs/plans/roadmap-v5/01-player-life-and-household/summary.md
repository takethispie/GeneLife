# Player Life and Household — Summary

**Folder:** `01-player-life-and-household/`
**Status:** Planned (V5 feature 01)

**Depends on:** V1 economy, housing and life stages, V2 movement and building interaction, V3 browser client and player identity, V4 family graph and inheritance, `PersistedWorldState` schema 11, `PlayerControlled`
**Feeds:** appearance and visual identity, social and multiplayer, live operations and scale

## Objective

Turn the player sim from a visitor into a full participant in the existing simulation. A player should be able to get a job, earn and spend money, rent and keep a home, form a family, age, die and continue through an heir — all through server-authoritative actions that reuse the V1–V4 domain rules.

This feature does not invent a second economy, a second family system or a client-side game loop. It exposes the systems that already exist to a player-controlled sim and adds the missing player-facing commands, read models and UI.

## Baseline before V5

- `PlayerControlled` sims are created on connect by `SimulationEngine.EnsurePlayerSim`.
- The only player intents are `MoveIntent` and `UseBuildingIntent`.
- Player sims are excluded from `DecisionSystem` and `WorkScheduleSystem`.
- A player can sleep at a property, work at their employer and study at a school, but cannot apply for a job, rent a home, shop, court, marry or have children.
- `Wallet`, universal basic income, payroll, rent, arrears and the ledger already exist for NPCs.
- V4 relationships, partnerships, pregnancy, birth, guardianship and inheritance are complete for NPCs and exposed through the admin family page.
- Player identity is a `playerId` string; the browser holds a JWT and the hub binds the player to a sim.
- `PersistedWorldState` is schema 11 and already persists `PlayerControlled`, `PlayerId` and `HasWallet`.

## Deliverables

1. Player agency policy and a typed player command/intent set.
2. Player employment: apply, accept, quit, attendance, wages and career progression.
3. Player housing: find a unit, rent, move, pay rent, fall into arrears and be evicted.
4. Player consumption: buy food and goods, satisfy needs and manage a wallet under the same ledger rules.
5. Player relationships and family actions: invite, consent, partner, conceive, give birth and raise a child.
6. Player aging, death, estate settlement and heir continuation.
7. Schema 12 persistence, player read models and the admin Player Life page.
8. Player-life acceptance tests, replay with a recorded intent log and a 32-player soak.

## Architecture

```
Browser (V3)
   │  typed intents: job, housing, shop, family, continuation
   ▼
WorldHub / HTTP command admission
   │
   ▼
CommandQueue ──► tick-start drain ──► PlayerActionService
                                         │
              ┌──────────────────────────┼──────────────────────────┐
              ▼                          ▼                          ▼
      EmploymentIndex             HousingRegistry              Family services
      Payroll/Attendance          Rent/Arrears/Eviction        Pairing/Pregnancy/Birth
              │                          │                          │
              └──────────────────────────┼──────────────────────────┘
                                         ▼
                              Ledger, events, read models
                                         │
                                         ▼
                              Player Life admin page
```

Player actions call the same domain services as NPC systems. The player is the decision source; the server remains the only writer.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Player agency and command contract](tech/01-player-agency-and-command-contract.md) | Foundation |
| 02 | [Player employment and income loop](tech/02-player-employment-and-income-loop.md) | Economy |
| 03 | [Player housing and household membership](tech/03-player-housing-and-household-membership.md) | Housing |
| 04 | [Player consumption and needs loop](tech/04-player-consumption-and-needs-loop.md) | Consumption |
| 05 | [Player relationships and family actions](tech/05-player-relationships-and-family-actions.md) | Family |
| 06 | [Player aging, death and heir continuation](tech/06-player-aging-death-and-heir-continuation.md) | Lifecycle |
| 07 | [Player persistence, read models and admin page](tech/07-player-persistence-read-models-and-admin-page.md) | Durability |
| 08 | [Player life acceptance](tech/08-player-life-acceptance.md) | Verification |

## Business reading

- [Why a playable life](business/01-why-a-playable-life.md)
- [Player agency without client authority](business/02-player-agency-without-client-authority.md)
- [Family continuity for players](business/03-family-continuity-for-players.md)

## Definition of done

- A new player can apply for a job, be hired, work, be paid and see the money in their wallet.
- A player can rent a unit, pay rent, move, fall into arrears and be evicted under the same rules as an NPC.
- A player can buy food and goods, and those purchases affect needs and the ledger.
- A player can invite an NPC or another player to a partnership, require consent, and form the relationship through the existing V4 edge rules.
- A player can conceive or accept a pregnancy, give birth and raise a child with inherited traits and family edges.
- A player sim ages, dies, settles its estate and continues through an eligible heir or the configured fallback.
- Every player action is a typed, rate-limited command with an audit result; the client never mutates world state.
- Schema 11 → 12 migration, restore and double restore preserve player state exactly.
- The admin Player Life page shows the player's job, wallet, household, family, action history and continuation state.
- Player-life acceptance, a 32-player soak and the existing 2,000-sim decade pass.

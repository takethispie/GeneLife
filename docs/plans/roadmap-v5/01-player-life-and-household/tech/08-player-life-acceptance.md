# P08 — Player life acceptance

**Phase:** 8 — Verification
**Depends:** P01–P07
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Prove that a player can live a complete life through server-authoritative actions without breaking the economy, family graph, persistence or the NPC simulation.

## Acceptance scenarios

### Fresh player

1. Connect a new player and assert a controlled sim with a valid profile, traits, wallet, needs and household.
2. Assert the player cannot submit commands for another sim.
3. Assert the player can move and use a building through the existing intents.

### Employment

1. Apply for an open posting at a reachable office.
2. Accept the offer, work a full pay cycle and assert wages and ledger.
3. Miss the attendance threshold and assert the existing consequence.
4. Quit and assert the seat is available to an NPC.

### Housing

1. Request and lease an available unit.
2. Pay rent for several cycles and assert the household balance.
3. Stop paying and assert arrears and eviction.
4. Move to a cheaper unit and assert no orphan occupant.

### Consumption

1. Buy food at a store and assert wallet, ledger and pantry.
2. Consume food and assert the need change.
3. Attempt a purchase with insufficient funds and assert no partial state.

### Family

1. Invite an eligible NPC, accept and assert mirrored partner edges.
2. Decline or expire an invitation and assert no edge.
3. Conceive, give birth and assert inherited traits, parent edges and household membership.
4. Assert a player partnership does not merge wallets.

### Death and continuation

1. Age a player sim to death with a living heir.
2. Assert estate settlement and heir continuation.
3. Repeat with `NewSim` and `Observer`.
4. Assert debts cannot be avoided by continuation choice.

### Persistence and replay

1. Save and restore a player with a job, home, family and pantry.
2. Double restore and compare.
3. Replay a recorded command log and compare the resulting player state.
4. Assert an NPC-only world still replays identically after V5.

### Scale

1. Run a 32-player soak with concurrent job, housing, shopping and family actions.
2. Assert per-player rate limits, command budgets and no world corruption.
3. Assert the 2,000-sim decade still meets the tick budget.

## Tasks

- [ ] Add the player-life acceptance suite under `Genelife.UnitTests/Player`.
- [ ] Add a recorded command-log fixture.
- [ ] Add the 32-player soak test.
- [ ] Add the player persistence and migration tests.
- [ ] Add the admin Player Life smoke test.
- [ ] Record results in `roadmap-v5/STATUS.md` when implemented.

## Files

- `Genelife.UnitTests/Player/PlayerLifeAcceptanceTests.cs`
- `Genelife.UnitTests/Player/PlayerCommandReplayTests.cs`
- `Genelife.UnitTests/Performance/PlayerSoakTests.cs`
- `Genelife.UnitTests/Fixtures/player-command-log.json`
- `docs/plans/roadmap-v5/STATUS.md`

## Acceptance

- A new player can complete the full job → home → consumption → family → death → continuation loop.
- Every action is server-authoritative and audited.
- Money is conserved and only the approved creation sources mint money.
- Player state survives save/restore and replay.
- The 32-player soak and 2,000-sim decade pass.
- No existing economy, housing, lifecycle or family test regresses.

## Testing

- Full player suite.
- Economy, housing, lifecycle, family and persistence suites.
- 32-player soak with 200 ticks and a full year.
- 2,000-sim decade with and without player activity.
- Live hub smoke test for admission, rejection and continuation.

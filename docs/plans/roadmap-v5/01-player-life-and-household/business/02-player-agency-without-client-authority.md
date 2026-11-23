# Player agency without client authority

A player-facing game makes it tempting to let the browser own convenience state: a local wallet, a predicted rent payment, an optimistic relationship flag. V5 rejects that shortcut.

## The rule

The browser sends a **choice**, not a result. The server validates the choice, applies it on the tick thread through the same domain service an NPC would use, writes the ledger and emits the event. The browser then sees the result in the read model or the next snapshot.

## Why it matters

- **Money.** A client-side debit can be replayed, skipped or forged. A ledgered server debit cannot.
- **Housing.** A client-side move can ignore capacity, tenure and arrears. `HousingRegistry` is the only membership path.
- **Family.** A client-side partnership can bypass consent, exclusivity and ancestry checks. `RelationshipService` owns those rules.
- **Death.** A client-side respawn can duplicate an estate. `EstateSettlementSystem` must settle exactly once.
- **Fairness.** NPCs and players should obey the same rules or the simulation stops being a simulation.

## What the player gets instead

- Fast acknowledgement: the command returns `202` with an id.
- A visible pending state for the action.
- A typed success or rejection with a reason.
- A read model that updates on the next tick.
- Rate limits that protect the world without hiding the reason.

## Disconnect policy

A disconnected player sim stays in the world and idles. It does not make autonomous job, housing, family or spending decisions. The player can reconnect and continue. This avoids a player returning to a life the simulation changed without them.

## What the client may do locally

- Camera, selection, hover and panel state.
- Local walkability hints and target previews.
- Optimistic UI that is clearly marked pending and is replaced by the server result.
- Appearance composition from server-provided descriptors.

The client never owns balances, positions, relationships, pregnancies, household membership or appearance truth.

# Operability and trust

A family simulation is easy to build and hard to trust. Traits, attraction, partnerships, pregnancies and inheritance all produce state that looks plausible even when it is wrong. Operability is what makes the difference visible.

## What the operator needs

- **A complete view.** Traits, parents, children, partners, guardians, pregnancies, households and costs in one place.
- **An explanation.** Why two sims paired, why a pair was rejected, why an inheritance split the way it did.
- **A health check.** Missing guardians, duplicate partners, ancestry cycles, broken trusts and stale projections.
- **A safe action path.** Force/end a partnership, cancel a pregnancy, assign a guardian; each audited and applied on the next tick.
- **No hidden model.** The attraction score is a list of named components, not a black box.

## Why trust matters more than features

A wrong family graph can:

- give an inheritance to the wrong sim;
- leave a child without a guardian;
- let a minor inherit and spend money;
- move a household into a unit that cannot hold it;
- create a partnership that should be impossible.

The admin must be able to find these defects before the player does. Metrics and invariants are not decoration; they are the safety net for a system with many cross-cutting dependencies.

## Read-model rules

- The live engine is authoritative for the current tick.
- Projections are the durable history and may lag.
- The admin shows a staleness warning and can fall back to live state.
- Every list is paginated and filtered on the server.
- The browser never accesses the world directly.

## Operation rules

- Every mutation is a typed command with an idempotency key.
- Every mutation returns `202` and applies at the next tick.
- Every mutation emits an event with the source `Admin`.
- A destructive operation (end partnership, cancel pregnancy, remove guardian) requires confirmation and is auditable.

## What the operator should not see

- A global attractiveness ranking.
- Trait sorting as a default.
- A player-facing partner search.
- A silent “fix” that changes family state without an event.

## Success signal

An operator can open the family page after a decade run, find a child without a guardian, see why the last pairing happened, and correct it without stopping the simulation or guessing at hidden state.
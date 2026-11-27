# Admin Endpoint Migration

## Context

V6's record: the legacy admin endpoints wait up to 50 ms for the next tick before responding. The newer admin endpoints already return `202 Accepted` with a command id, so the codebase contains two answers to the same question and the operator plane's behaviour depends on which endpoint an operator happens to call.

V6 also notes a related fix: legacy admin mutations now go through typed admin commands. This step closes the remaining half — the response shape and the blocking wait.

## Design

1. **Inventory first.** The record does not enumerate the blocking endpoints. Find them by the wait itself rather than by guessing from route names: locate the code path that waits for the next tick, then list every endpoint that reaches it. The deliverable of this half is a table of endpoint -> command -> owner, agreed before any code moves.
2. **Migrate mechanically.** For each endpoint, drop the wait and return `202 Accepted` plus the command id, following the newer endpoints exactly. The command surface already exists, so no new command types are expected; if one is needed, that is a signal the inventory was incomplete.
3. **Decide, do not drift, on the exceptions.** Any endpoint that genuinely must answer synchronously — a read-back an operator UI depends on, or a decision the next tick needs — gets a recorded reason and stays. The failure mode to avoid is leaving the rest unmigrated by omission.
4. **Do not touch the simulation path.** Commands are still drained at tick start under the write lock. Only the HTTP response changes.

## Tasks

- [ ] Locate the wait-for-next-tick path and enumerate every endpoint that reaches it.
- [ ] Record the endpoint -> command mapping, including any endpoint with no typed command yet.
- [ ] Migrate the endpoints to `202 Accepted` + command id, matching the newer pattern.
- [ ] For each endpoint left synchronous, record the reason.
- [ ] Confirm no admin endpoint waits on the tick after the change, and that the command is still applied exactly once.

## Files

- `Genelife.API/Endpoints/` (legacy admin endpoints and their Wolverine counterparts)
- `Genelife.Application/Commands/AdminCommand.cs`
- `Genelife.Application/Services/SimulationEngine.cs` (command application only)

## Contracts

- Response shape becomes `202 Accepted` + command id for migrated endpoints.
- Commands remain applied at tick start, exactly once, under the write lock.
- No simulation behaviour changes.

## Acceptance

- No admin endpoint blocks waiting for the next tick, except those with a recorded reason.
- The endpoint -> command inventory exists and matches the code.
- A migrated endpoint returns a command id that can be used to observe the outcome.

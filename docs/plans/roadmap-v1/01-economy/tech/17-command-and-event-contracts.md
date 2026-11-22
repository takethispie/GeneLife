# E17 — Commands and events

**Phase:** 10 — Plumbing
**Depends:** E03, E04, E05
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Define one safe, typed path for every economy mutation and one event vocabulary for downstream projections. Replace the legacy pipe-delimited command payload.

## Context

`SimulationEngine` drains `EnqueuedCommand` with a `string Payload` split on `|` (`SimulationEngine.cs:349`). `CreateJobPosting` is declared as a message but never handled. Admin endpoints currently call `SimulationManager`, which mutates the world under a lock rather than on the tick thread. Adding dozens of economy commands on top of that would multiply the risk.

## Design

### Typed admin commands

```csharp
public abstract record AdminCommand(Guid CommandId, string IssuedBy);

public sealed record CreateOfficeCommand(Guid CommandId, string IssuedBy,
    ulong CompanyId, string Name, int Capacity, Money OperatingCost) : AdminCommand(CommandId, IssuedBy);

public sealed record OpenJobPostingCommand(...) : AdminCommand(...);
public sealed record InjectCapitalCommand(...) : AdminCommand(...);
public sealed record SetHiringFreezeCommand(...) : AdminCommand(...);
// ...
```

A bounded `Channel<AdminCommand>` lives on `SimulationEngine`. `DrainCommands` executes them at the start of the tick, before systems, and writes a `CommandResult` (accepted/rejected + reason) to a small result cache. HTTP returns `202` with the command id; `GET /api/commands/{id}` returns the result. The UI can also poll the read model.

The existing `ICommandQueue`/`Intent` channel remains the player-intent path. Admin commands are a separate, typed channel and do not reuse `Intent`; this avoids mixing authorization models and untrusted player input with operator actions.

### Rules

- Commands are validated twice: syntactically at the endpoint, semantically in the apply phase.
- Commands are idempotent by `CommandId`; duplicate delivery is ignored.
- Commands never run inside a query and never perform I/O.
- Only admin-authorized endpoints can enqueue.
- Existing `SimulationManager` write methods stay for compatibility but new features do not use them.

### Event vocabulary

Events are immutable records in `Genelife.Messages/Events`, named in past tense, with `NetworkId` rather than `Entity` so they survive persistence.

Minimum economy set:

`OfficeCreated`, `OfficeUpdated`, `OfficeClosed`, `JobPostingOpened`, `JobPostingClosed`, `JobSeatReserved`, `JobSeatReleased`, `EmployeeHired` (exists), `RecruitmentFeeCharged`, `EmployeeFired` (exists), `EmployeeResigned`, `SeverancePaid`, `WagesPaid`, `WagesDeferred`, `PayrollShortfall`, `WageArrearsSettled`, `ConsumerSpendBooked`, `UnmetDemandRecorded`, `CompanyRevenueBooked`, `TaxPaid`, `CapitalInjected`, `LoanIssued`, `LoanPaymentMade`, `LoanDefaulted`, `CompanyDistressed`, `CompanyRestructured`, `CompanyLiquidated`, `LedgerEntryRecorded`.

## Tasks

- [ ] Add `AdminCommand` hierarchy and command channel.
- [ ] Add `CommandResult` cache and `GET /api/commands/{id}`.
- [ ] Execute commands at the start of `Tick`, before systems, under the world lock.
- [ ] Add endpoint helpers that validate and enqueue; return `202`.
- [ ] Replace `EnqueuedCommand` string payload for new commands; keep legacy path until E18 removes it.
- [ ] Add event records and projection handlers for the economy set.
- [ ] Add message contract tests (serialization, required fields, no `Entity`).
- [ ] Document command/event ownership per step.

## Files

- new `Genelife.API/Commands/AdminCommand.cs`, `CommandResult.cs`, `AdminCommandQueue.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Services/EnqueuedCommand.cs`
- `Genelife.Messages/Events/*`
- `Genelife.API/Endpoints/Wolverine/*`

## Acceptance

- Every economy mutation from the admin is applied at a tick boundary.
- Duplicate command ids do not duplicate effects.
- Rejected commands return a reason and change nothing.
- No event contains an Arch `Entity`.
- Serialization round-trip tests pass for every command and event.
- The legacy string-payload path is no longer used by new endpoints.

## Testing

- Command idempotency test.
- Rejection path test.
- Command-before-systems ordering test.
- Event serialization snapshot tests.

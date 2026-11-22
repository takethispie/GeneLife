# E06 — Ledger and transactions

**Phase:** 3 — Money
**Depends:** E01, E05
**Estimate:** 2–3 sessions
**Status:** Implemented (V1)

## Goal

Record every money movement as an immutable, idempotent ledger entry so balances are explainable and restore can be reconciled.

## Context

Money currently changes by direct field assignment. There is no audit trail and no way to detect duplication after retries. `SimulationEventWriter` already demonstrates the right pattern: tick thread enqueues events, a background service writes to Marten (`Genelife.API/Services/SimulationEventWriter.cs`).

## Design

```csharp
public enum LedgerKind
{
    CapitalInjected, WagesPaid, WagesDeferred, WageArrearsSettled,
    ConsumerSpending, CompanyRevenue, OfficeCost, RentPaid, RentArrears,
    UtilityPaid, TaxPaid, PensionContribution, PensionPaid,
    RecruitmentCost, SeverancePaid, LoanIssued, LoanInterest, LoanPrincipal,
    PropertyPurchase, ConstructionCost, Inheritance, WriteOff, Grant
}

public readonly record struct LedgerEntry(
    Guid TransactionId,
    long Tick,
    LedgerKind Kind,
    ulong? FromNetworkId,
    ulong? ToNetworkId,
    Money Amount,
    string Reference,
    string? Memo);
```

`FinanceLedger` singleton:

- `Record(...)` appends to a bounded ring buffer (last 10,000) for the admin;
- enqueues the entry to `SimulationEngine.OutboundEvents` for Marten persistence;
- exposes `IReadOnlyList<LedgerEntry> Recent(int count, ulong? entityId)`.

`LedgerWriter` (background service) appends entries to a Marten event stream in batches. Event store is the source of truth; the ring buffer is a cache.

Double-entry rule:

- A transfer debits one account and credits another with the same transaction id.
- Sources (`CapitalInjected`, `Grant`) and sinks (`WriteOff`) are allowed only through explicit kinds and are visible in money-supply metrics.
- Every mutation method takes a `transactionId`; retries return the existing entry instead of duplicating it.

The tick thread never performs I/O.

## Tasks

- [ ] Add `LedgerKind`, `LedgerEntry`, `FinanceLedger`.
- [ ] Add `LedgerEntryRecorded` message and Marten append in `LedgerWriter`.
- [ ] Route all existing money mutations (payroll, seed capital) through `FinanceLedger`.
- [ ] Add idempotency dictionary with bounded eviction (last 100k transaction ids).
- [ ] Add `GET /api/ledger?entityId=&kind=&fromTick=&toTick=&take=` (admin).
- [ ] Add a reconciliation check comparing account balances with ledger sums in debug builds.
- [ ] Emit a metric when a source/sink entry is recorded.

## Files

- new `Genelife/Components/Finance/LedgerEntry.cs`, `LedgerKind.cs`
- new `Genelife.API/Services/FinanceLedger.cs`, `LedgerWriter.cs`
- `Genelife.API/Program.cs`
- `Genelife.API/Endpoints/Wolverine/LedgerEndpoints.cs`
- `Genelife.Messages/Events/LedgerEntryRecorded.cs`

## Acceptance

- Every company and wallet balance can be reconstructed from ledger entries.
- Replaying the same transaction id twice produces one entry and one balance change.
- Ledger writes never block a tick; backlog is dropped with a visible warning.
- Admin can filter entries by entity and kind.
- Debug reconciliation passes after a 10,000-tick run.

## Testing

- Idempotency test.
- Conservation test: sum of debits equals sum of credits per transaction.
- Ring-buffer eviction and query test.
- Reconciliation test after payroll and revenue cycles.

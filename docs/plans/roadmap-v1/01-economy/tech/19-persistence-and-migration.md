# E19 — Persistence and migration

**Phase:** 11 — Durability
**Depends:** E05, E06, E13, E15, E17
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Persist all economy state and rebuild it faithfully after restart: accounts, offices, postings, seats, applications, employment, loans, fiscal accounts and the ledger.

## Context

Current persistence captures only sims and companies (`PersistedWorldState`), and restore recreates companies as bare `CompanyName` entities (`SimulationEngine.Restore`, `SimulationEngine.cs:851`). Employment, offices, postings and money are lost. `SimDocument` stores needs and action only. The snapshot schema version is `1`; new fields require a version bump and a migration path.

## Design

### Snapshot v2

`PersistedWorldState.CurrentSchemaVersion = 2`.

```csharp
public sealed class PersistedWorldState
{
    public int SchemaVersion { get; set; }
    public long Tick { get; set; }
    public int RandomSeed { get; set; }
    public List<PersistedSim> Sims { get; set; }
    public List<PersistedCompany> Companies { get; set; }
    public List<PersistedOffice> Offices { get; set; }
    public List<PersistedPosting> Postings { get; set; }
    public List<PersistedApplication> Applications { get; set; }
    public List<PersistedLoan> Loans { get; set; }
    public PersistedFiscal Fiscal { get; set; }
}
```

- `PersistedCompany` gains: `AccountBalanceCents`, `ReservedCents`, `ArrearsCents`, `Solvency`, `StateChangedTick`, `LossCarryForwardCents`.
- `PersistedOffice` adds: `NetworkId`, `CompanyNetworkId`, `Name`, `Capacity`, `OperatingCostCents`, `IsClosed`.
- `PersistedPosting` adds: `NetworkId`, `CompanyNetworkId`, `OfficeNetworkId`, `Position`, `SalaryCents`, `Requirements`, `OpenSeats`, `PostedAtTick`.
- `PersistedApplication` adds applicant, posting, company, position, status, match score and date.
- Employment is a relation on `PersistedSim`: `EmployerNetworkId`, `OfficeNetworkId`, `Position`, `SalaryCents`.
- `PersistedFiscal` contains government and pension fund balances and pension accounts.

### Restore order

1. Create companies with accounts.
2. Create offices and link to companies.
3. Create postings and seats.
4. Create fiscal singletons.
5. Create sims, wallets, stages and skills.
6. Rebuild employment relations and `EmploymentIndex`.
7. Restore applications and loans.
8. Restore tick and random seed.
9. Run `Reconcile()`: every account equals its ledger sum; every employee appears once in the index; every seat belongs to a live posting.

`NetworkIdRegistry` is populated before any relation is restored. Never persist `Entity` values.

### Marten documents

| Document | Fields added |
|---|---|
| `SimDocument` | stage, age ticks, wallet cents, savings cents, employer id, housing unit id, health, skill profile |
| `CompanyDocument` | account balance, revenue, wages, arrears, solvency |
| new `OfficeDocument` | company id, name, capacity, cost, seats |
| new `JobPostingDocument` | company, office, salary, seats, state |
| new `LedgerEntryDocument` | full ledger entry |
| new `EconomySnapshotDocument` | yearly metrics |

Projections stay inline for entity documents; ledger and metrics use the async writer.

### Migration

- Detect v1 snapshots and upgrade in memory: seed accounts with `InitialCompanyCapital`, create one default office per company, convert float wallets to cents.
- Write the upgraded snapshot as v2 on next save.
- Keep the v1 reader for one release cycle.

## Tasks

- [ ] Bump schema to 2 and add all persisted sections.
- [ ] Persist and restore employment, offices, postings, applications, loans, fiscal accounts.
- [ ] Add `Reconcile()` and fail restore on invariant violations.
- [ ] Extend `WorldSaveService` and `WorldRestoreService` for the new documents.
- [ ] Add `LedgerEntryDocument` and `EconomySnapshotDocument` projections.
- [ ] Implement v1 → v2 migration.
- [ ] Add round-trip tests for every new section.
- [ ] Add a restart scenario test: run 1,000 ticks, save, restore, run 1,000 more, compare invariants.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Services/SimulationEngine.cs` (`CaptureState`, `Restore`)
- `Genelife.API/Services/WorldSaveService.cs`, `WorldRestoreService.cs`
- `Genelife.API/Models/SimDocument.cs`, `CompanyDocument.cs`, new documents
- `Genelife.API/Projections/*`
- `Genelife.UnitTests/Integration/PersistenceTests.cs`

## Acceptance

- Save/restore preserves balances to the cent, employment, postings, seats and applications.
- Restored indices match recomputed values.
- A v1 snapshot upgrades without data loss and is saved as v2.
- Restore refuses a corrupt snapshot with a specific error.
- 2,000-tick continuous run and save/restore run produce the same invariant state.
- Ledger and balances reconcile after restore.

## Testing

- Full snapshot round-trip.
- v1 migration test with a checked-in fixture.
- Corrupt-state rejection test.
- Restart continuation test.
- Projection rebuild test.

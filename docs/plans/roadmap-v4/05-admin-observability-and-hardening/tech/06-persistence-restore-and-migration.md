# H06 — Persistence, restore and migration

**Phase:** 6 — Durability
**Depends:** T06, R01–R11, F01–F11, C01–C07
**Estimate:** 2–3 sessions
**Status:** Planned (V4)

## Goal

Persist the complete family graph, pregnancy/trust/guardianship state and options, and migrate schema 5 saves without losing or inventing family facts.

## Context

`PersistedWorldState.CurrentSchemaVersion` moves from 5 to 6 in T06 for traits. V4 adds relationships, pregnancies, trusts, state care and family options. The 2026-11 `BalanceBackfilled` migration and persisted economy/housing options must be preserved.

## Design

### Schema 6 additions

`PersistedSim`:

- trait fields and `TraitsMigrated` (T06).

`PersistedWorldState`:

```csharp
public List<PersistedRelationship> Relationships { get; set; } = [];
public List<PersistedPregnancy> Pregnancies { get; set; } = [];
public List<PersistedTrust> Trusts { get; set; } = [];
public List<PersistedStateCare> StateCare { get; set; } = [];
public Genelife.Family.GeneticsOptions? GeneticsOptions { get; set; }
public Genelife.Family.RelationshipOptions? RelationshipOptions { get; set; }
public Genelife.Family.ReproductionOptions? ReproductionOptions { get; set; }
public Genelife.Family.InheritanceOptions? InheritanceOptions { get; set; }
public string AttractionModelVersion { get; set; } = "v4-attraction-1";
```

`PersistedRelationship`:

```csharp
public ulong NetworkId { get; set; }
public ulong FromSimNetworkId { get; set; }
public ulong ToSimNetworkId { get; set; }
public string Kind { get; set; } = string.Empty;
public string Source { get; set; } = string.Empty;
public long SinceTick { get; set; }
public long? EndedTick { get; set; }
```

`PersistedPregnancy`:

```csharp
public ulong NetworkId { get; set; }
public ulong CarryingParentNetworkId { get; set; }
public ulong OtherParentNetworkId { get; set; }
public long ConceivedTick { get; set; }
public long DueTick { get; set; }
public int RiskBasisPoints { get; set; }
public string Status { get; set; } = string.Empty;
public string Source { get; set; } = string.Empty;
public int ChildrenExpected { get; set; }
public long CarePaidCents { get; set; }
```

`PersistedTrust`:

```csharp
public ulong NetworkId { get; set; }
public ulong BeneficiaryNetworkId { get; set; }
public ulong GuardianNetworkId { get; set; }
public ulong SourceSimNetworkId { get; set; }
public long BalanceCents { get; set; }
public long CreatedTick { get; set; }
public long ReleaseAgeTicks { get; set; }
```

`PersistedStateCare`:

```csharp
public ulong ChildNetworkId { get; set; }
public long SinceTick { get; set; }
public long MonthlyCostCents { get; set; }
```

### Restore order

1. Initialize map/options.
2. Restore map, placements, sims, companies, offices, postings, properties, units, households, schools.
3. Restore relationships, pregnancies, trusts and state care.
4. Rebuild `RelationshipIndex`, `PregnancyIndex`, housing and lifecycle indices.
5. Run the legacy balance backfill if needed.
6. Repair/verify family invariants and fail loudly on impossible state.

### Migration rules

- V5 → V6 backfills traits deterministically.
- Do not invent parent edges for legacy households; mark restored roles as `Migration` and use the household head as guardian fallback.
- Preserve `BalanceBackfilled`, economy/housing options and the 2026-11 one-time backfill.
- Migrate relationship/pregnancy/trust rows only if they exist; older saves have none.
- A second restore must not change any family fact.

### Snapshot size

- Measure at 2,000 sims with relationships and pregnancies.
- If size grows beyond the budget, compact enum strings to ids in a later schema; do not change field names without a migration.

### Failure handling

- Unknown enum/kind/status: fail with the entity network id.
- Dangling relationship endpoint: fail with both ids.
- Trust beneficiary/guardian missing: fail.
- Pregnancy parent missing: fail.
- Do not silently drop a family fact.

## Tasks

- [ ] Add the V6 persisted types and lists.
- [ ] Capture relationships, pregnancies, trusts and state care.
- [ ] Restore and rebuild all family indices.
- [ ] Add the V5 migration and legacy-family marker.
- [ ] Preserve the 2026-11 backfill and options.
- [ ] Add save/restore, double-restore, corruption and size tests.
- [ ] Add a legacy fixture with households but no family edges.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Persistence/FamilyPersistenceTests.cs`
- `Genelife.UnitTests/Fixtures/v5-legacy-family.json`
- `docs/plans/roadmap-v4/STATUS.md`

## Acceptance

- A V6 save round-trips traits, relationships, pregnancies, trusts and state care exactly.
- Restore rebuilds indices to the same lookups.
- Double restore changes nothing.
- A V5 save migrates deterministically and stays loadable.
- Legacy households are flagged, not given invented parent edges.
- Unknown/dangling family state fails loudly.
- Snapshot size is within budget at 2,000 sims.

## Testing

- Save/restore a full family decade.
- Restore twice and compare.
- V5 fixture migration.
- Corrupt each family row and assert the error.
- Snapshot size at 2,000.
- Continue 1,000 ticks after restore and compare invariants.
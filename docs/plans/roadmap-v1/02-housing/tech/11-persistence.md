# H11 — Persistence

**Phase:** 8 — Durability
**Depends:** H01–H10, E19
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Persist properties, units, households, tenancies, arrears and construction projects, and rebuild the housing registry exactly after restart.

## Context

The snapshot schema gains a housing section. As with employment, Arch `Entity` references cannot be persisted; every relation must use `NetworkId`.

## Design

### Snapshot additions

```csharp
public List<PersistedProperty> Properties { get; set; }
public List<PersistedHousingUnit> Units { get; set; }
public List<PersistedHousehold> Households { get; set; }
public List<PersistedConstructionProject> ConstructionProjects { get; set; }
public HousingMarketState Market { get; set; }
```

`PersistedProperty`:

- `NetworkId`, name, district, owner network id (0 = municipality), unit count, condition, neighbourhood, upkeep, shelter flag.

`PersistedHousingUnit`:

- `NetworkId`, property network id, unit number, tier, capacity, base rent cents, condition, vacancy, current household id.

`PersistedHousehold`:

- `NetworkId`, head sim network id, unit network id, tenure, monthly rent cents, move-in tick, arrears cycles, rent arrears cents, utility arrears cents, member sim ids.

Construction:

- project id, developer id, district, tier, units, cost, months remaining, start tick, paid-to-date.

### Restore order

1. properties;
2. units and ownership links;
3. households and members;
4. tenancies (`Household.Unit`, `HousingUnit.IsVacant`);
5. construction projects;
6. housing market index and district capacity;
7. rebuild `HousingRegistry`;
8. rebuild `Residence` for every member;
9. run consistency checks.

### Marten documents

- `PropertyDocument`, `HousingUnitDocument`, `HouseholdDocument`, `ConstructionProjectDocument`.
- `SimDocument` gains `HouseholdNetworkId`, `HousingUnitNetworkId`, `Tenure`, `HousingTier`.
- Projections are inline for entity documents; construction updates use the async writer.

### Migration

v1 snapshots have no housing. On upgrade, assign every adult sim to a generated household and a unit using `HouseholdAssigner` with the persisted random seed. Mark the migration in the log and emit `HousingMigrated`.

## Tasks

- [ ] Add snapshot sections and bump schema.
- [ ] Extend `CaptureState`/`Restore` with housing and construction.
- [ ] Add documents and projections.
- [ ] Implement registry and `Residence` rebuild with checks.
- [ ] Implement the v1 → v2 housing migration.
- [ ] Add round-trip tests for all housing relations.
- [ ] Add a corrupt-state test (unit with two households).
- [ ] Persist and restore market index and land capacity.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Models/*` new documents
- `Genelife.API/Projections/*`
- `Genelife.UnitTests/Integration/PersistenceTests.cs`

## Acceptance

- Save/restore preserves every tenancy, member, arrear and construction project.
- Registry and `Residence` match after restore.
- No unit has two households after restore.
- Housing market index and rent levels survive restart.
- v1 data migrates without losing sims and produces valid housing.
- Corrupt snapshots are rejected with a specific error.

## Testing

- Full housing round-trip.
- Registry rebuild equality.
- Migration fixture test.
- Corruption rejection test.
- Restart continuation scenario.

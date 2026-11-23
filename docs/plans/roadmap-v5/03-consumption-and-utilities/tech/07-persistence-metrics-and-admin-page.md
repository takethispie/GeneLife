# C07 — Persistence, metrics and admin page

**Phase:** 7 — Durability
**Depends:** C01–C06, schema 13
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Persist utility, grocery and nutrition state, expose consumption metrics and give operators a Consumption page.

## Context

Schema 13 adds appearance. Schema 14 adds utility networks, service links, household meters, grocery inventory and nutrition. The admin has Economy and Housing pages but no view of infrastructure load, outages, stock or nutrition.

## Design

### Schema 14

`PersistedWorldState.CurrentSchemaVersion` moves from 13 to 14.

New persisted blocks:

```csharp
public sealed class PersistedUtilityNetwork
{
    public long NetworkId { get; set; }
    public UtilityService Service { get; set; }
    public int Production { get; set; }
    public int Capacity { get; set; }
    public UtilityNetworkState State { get; set; }
}

public sealed class PersistedServiceLink
{
    public long BuildingNetworkId { get; set; }
    public long UtilityNetworkId { get; set; }
    public UtilityService Service { get; set; }
    public int Demand { get; set; }
}

public sealed class PersistedHouseholdMeter
{
    public long HouseholdNetworkId { get; set; }
    public int WaterUsage { get; set; }
    public int ElectricityUsage { get; set; }
    public ServiceLevel WaterLevel { get; set; }
    public ServiceLevel ElectricityLevel { get; set; }
    public long ArrearsCents { get; set; }
}
```

Grocery inventory and nutrition are persisted per store and household. The catalog version is persisted once.

### Migration

- Schema 13 worlds get one network per service and links for served buildings.
- Existing households get meters with zero usage and full service.
- Grocery buildings get initial stock; other buildings get none.
- No household is silently cut off by migration.
- A second restore produces identical state.

### Metrics

`ConsumptionMetricsSystem` (order 632) records:

- production, capacity, load and reserve per service;
- outage count and duration;
- utility revenue, arrears and cut-offs;
- grocery stock, stock-outs, spoilage and revenue;
- nutrition distribution and malnutrition count;
- household utility and grocery cost per capita.

### Admin page

`/consumption` supports:

- network overview and state;
- service links by building;
- household meters, arrears and service level;
- outage history and causes;
- grocery stock, prices, spoilage and restock;
- nutrition distribution and poor-nutrition households;
- manual repair/restock actions with audit.

## Tasks

- [ ] Add the schema 14 persisted blocks and migration.
- [ ] Persist the catalog version and store inventory.
- [ ] Add `ConsumptionMetricsSystem` and metrics DTOs.
- [ ] Add `/api/consumption` endpoints with admin authorization.
- [ ] Add the `/consumption` admin page with tabs and filters.
- [ ] Add manual repair/restock actions with audit.
- [ ] Add migration, restore and double-restore tests.
- [ ] Add HTTP smoke tests for unauthorized access.

## Files

- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/Persistence/SnapshotMigrations.cs`
- new `Genelife.API/Consumption/ConsumptionReadModel.cs`
- new `Genelife.API/Endpoints/ConsumptionEndpoints.cs`
- new `Genelife.Web/Pages/Consumption.razor`
- `Genelife.UnitTests/Persistence/ConsumptionPersistenceTests.cs`

## Acceptance

- Schema 13 saves migrate to 14 and restore exactly.
- A second restore does not change meters, stock or nutrition.
- No household is silently cut off by migration.
- Metrics cover networks, outages, grocery and nutrition.
- The admin page shows the full consumption picture and supports safe actions.
- All consumption routes are admin-only.
- Events have idempotent projections.

## Testing

- Migrate a schema 13 save and compare networks, links, meters and stock.
- Double restore and compare.
- Rebuild projections and compare.
- Assert unauthorized `/api/consumption` returns 401/403.
- Assert the page renders with empty, seeded and migrated worlds.
- Assert manual actions are audited.

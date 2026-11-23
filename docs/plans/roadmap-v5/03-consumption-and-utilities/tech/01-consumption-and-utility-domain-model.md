# C01 — Consumption and utility domain model

**Phase:** 1 — Foundation
**Depends:** V1 housing/households, feature 01 pantry
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Define the shared vocabulary for utility services, service links, household meters, service levels, grocery stock and nutrition so later steps do not invent parallel models.

## Context

Utilities are currently a single `UtilityBill` computed by `HousingCostPolicy` and charged by `RentSystem`. There is no service type, network, meter, capacity or outage. Grocery is a minimal spend with no stock or nutrition. Feature 01 adds a household pantry for player purchases.

The domain model must support both player and NPC households and remain cheap enough for the tick loop.

## Design

### Utility services

```csharp
public enum UtilityService : byte { Water, Electricity }

public enum ServiceLevel : byte { Full, Reduced, CutOff }

public enum UtilityNetworkState : byte { Normal, Strained, Overloaded, Offline }
```

### Components

```csharp
public record struct UtilityNetwork
{
    public UtilityService Service;
    public int Production;
    public int Capacity;
    public int Load;
    public int Reserve;
    public UtilityNetworkState State;
}

public record struct BuildingServiceLink
{
    public Entity Network;
    public UtilityService Service;
    public int Demand;
    public bool Connected;
}

public record struct HouseholdMeter
{
    public int WaterUsage;
    public int ElectricityUsage;
    public ServiceLevel WaterLevel;
    public ServiceLevel ElectricityLevel;
    public Money Arrears;
    public long LastReadingTick;
}
```

### Grocery and nutrition

```csharp
public enum GroceryCategory : byte { Staple, Produce, Protein, Prepared, Household }

public sealed record GroceryCatalogItem
{
    public string Id { get; init; }
    public GroceryCategory Category { get; init; }
    public int Nutrition { get; init; }
    public int ShelfLifeTicks { get; init; }
    public Money BasePrice { get; init; }
    public int UnitsPerPurchase { get; init; }
}

public record struct StoreInventory
{
    public int ItemIndex;
    public int Stock;
    public Money CurrentPrice;
    public long LastRestockTick;
}

public record struct NutritionState
{
    public int Balance;
    public int ConsecutivePoorDays;
    public long LastMealTick;
}
```

### Rules

- One authoritative network per service in V5a.
- A building connects to a network through a service link created at placement or migration.
- A household has one meter per service.
- Usage and stock are integers; money is `Money.Cents`.
- No negative stock, usage or arrears.
- Bills and purchases are ledgered transfers. Spoilage and outages destroy service or goods, never money.
- Nutrition is a modifier on existing health and needs, not a second health model.

## Tasks

- [ ] Add utility enums, network, link, meter and service level.
- [ ] Add grocery catalog, inventory and nutrition types.
- [ ] Add validation for non-negative values and defined enums.
- [ ] Add the catalog fixture and a store inventory factory.
- [ ] Document the one-network-per-service assumption.
- [ ] Add value-size and round-trip tests.
- [ ] Add conservation rules to the domain docs.

## Files

- new `Genelife/Components/Consumption/UtilityNetwork.cs`
- new `Genelife/Components/Consumption/BuildingServiceLink.cs`
- new `Genelife/Components/Consumption/HouseholdMeter.cs`
- new `Genelife/Components/Consumption/NutritionState.cs`
- new `Genelife/Consumption/GroceryCatalog.cs`
- new `Genelife/Consumption/StoreInventory.cs`
- `Genelife.UnitTests/Consumption/ConsumptionDomainTests.cs`

## Acceptance

- Every enum and component round-trips through persistence.
- Validation rejects negative usage, stock and arrears.
- A household has at most one meter per service.
- A building has at most one link per service.
- The grocery catalog is deterministic and versioned.
- No money is created or destroyed by the domain types.

## Testing

- Serialize each component and compare.
- Assert a duplicate meter or link is rejected.
- Assert a negative stock is rejected.
- Assert the catalog fixture is stable.
- Assert the domain model has no float money.

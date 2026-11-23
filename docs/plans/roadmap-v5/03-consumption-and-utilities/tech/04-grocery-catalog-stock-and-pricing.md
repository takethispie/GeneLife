# C04 — Grocery catalog, stock and pricing

**Phase:** 4 — Grocery
**Depends:** C01, V1 company revenue, feature 01 pantry
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Turn grocery from a minimal spend into a catalog with store stock, deterministic prices and spoilage, shared by player and NPC households.

## Context

NPC households spend through `ConsumerSpendingSystem` and a minimal grocery store. Feature 01 adds a household pantry for player purchases. There is no catalog, inventory, restock or spoilage.

V5a uses a deterministic store catalog. It does not add player-run grocery chains or a full supply chain.

## Design

### Catalog

- `GroceryCatalog` is a versioned list of `GroceryCatalogItem`.
- Each item has a category, nutrition, shelf life, base price and units per purchase.
- The catalog is loaded from configuration and persisted with a version.
- Prices are deterministic from base price, store tier, stock pressure and season.

### Store inventory

```csharp
public record struct StoreInventory
{
    public int ItemIndex;
    public int Stock;
    public Money CurrentPrice;
    public long LastRestockTick;
}
```

- Each grocery building owns a small inventory array.
- Stock is capped by store tier.
- Restock runs daily from a configured supplier at a ledgered cost.
- A store with zero stock cannot sell that item.

### Pricing

`GroceryPricingPolicy` computes:

```
price = basePrice * tierMultiplier * stockPressure * seasonMultiplier
```

- All factors are integer basis points.
- The price is clamped to a configured floor and ceiling.
- The same inputs produce the same price.

### Spoilage

`GrocerySpoilageSystem` (order 476) runs daily:

- pantry and store stock age by item shelf life;
- spoiled units are removed;
- a `GrocerySpoiled` event records the quantity and reason;
- spoilage never creates or destroys money.

### Store revenue

- A purchase debits the household and credits the store's company or government account.
- The ledger records `GroceryPurchase`.
- The store's restock cost is a real expense, not a mint.

## Tasks

- [ ] Add the versioned grocery catalog and configuration.
- [ ] Add store inventory creation for grocery buildings.
- [ ] Implement deterministic pricing and stock pressure.
- [ ] Add daily restock with a ledgered supplier cost.
- [ ] Add spoilage for store and pantry stock.
- [ ] Add `GroceryPurchase` and `GrocerySpoiled` events.
- [ ] Add admin stock, price and spoilage read fields.
- [ ] Add tests for pricing, stock-out, restock and spoilage.

## Files

- new `Genelife/Consumption/GroceryCatalog.cs`
- new `Genelife/Consumption/GroceryPricingPolicy.cs`
- new `Genelife/Systems/GroceryStockSystem.cs`
- new `Genelife/Systems/GrocerySpoilageSystem.cs`
- `Genelife/Systems/ConsumerSpendingSystem.cs`
- `Genelife.UnitTests/Consumption/GroceryTests.cs`

## Acceptance

- Every grocery building has inventory for the configured catalog.
- A purchase reduces stock and credits the store.
- A zero-stock item cannot be sold.
- Restock is funded and ledgered.
- Spoilage removes stock and emits an event.
- Prices are deterministic and clamped.
- Player and NPC purchases use the same catalog and stock.

## Testing

- Buy an item and assert stock, wallet and ledger.
- Sell out and assert rejection.
- Restock and assert stock and supplier cost.
- Advance shelf life and assert spoilage.
- Change stock pressure and assert the price moves within bounds.
- Replay a day and compare prices and stock.

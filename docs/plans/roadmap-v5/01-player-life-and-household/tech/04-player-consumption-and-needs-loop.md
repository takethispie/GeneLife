# P04 — Player consumption and needs loop

**Phase:** 4 — Consumption
**Depends:** P01, P02, P03, V1 consumer spending and ledger, V3 needs
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Let a player buy food and basic goods, carry them in a household pantry and consume them to satisfy needs, using the same wallet and ledger rules as an NPC household.

## Context

NPC households spend through `ConsumerSpendingSystem` and a minimal grocery store. Needs decay for living sims. Player sims have a wallet and needs but no purchase or consume action, so they cannot manage hunger or household supplies.

Feature 03 adds the advanced grocery catalog, stock, pricing and nutrition. Feature 01 needs a small, stable purchase/consume loop that works before that depth lands and can be extended by it.

## Design

### Household pantry

```csharp
public record struct HouseholdPantry
{
    public int FoodUnits;
    public int HouseholdGoods;
    public long LastPurchaseTick;
}
```

The pantry belongs to the household, not the player sim. A player contributes purchases to the household and consumes from it.

### Commands

- `BuyItem` with `StoreNetworkId`, `CatalogItemId` and `Quantity`.
- `ConsumeItem` with `CatalogItemId` and `Quantity`.

### Purchase flow

1. The player must be at the store entrance.
2. The catalog item must exist and the store must have stock (feature 03) or the basic catalog entry must be available.
3. The price is computed by the existing pricing policy.
4. The wallet is debited and the store/company is credited through the ledger.
5. The pantry is incremented in the apply phase.
6. Insufficient funds reject with `insufficient_funds`; no negative wallet.

### Consume flow

1. The player must be at home or in a valid eating location.
2. The pantry must have the item.
3. The item is decremented and the need effect is applied.
4. A `PlayerConsumedItem` event is emitted.
5. No money moves on consumption.

### Needs and failure

- Hunger and other needs decay under the existing needs system.
- A player with no food can still buy at a store or use a civic service if one exists.
- A player who cannot afford food experiences the same need and health consequences as an NPC.
- Initial company capital, initial household cash and universal basic income remain the only approved creation sources.

## Tasks

- [ ] Add `HouseholdPantry`.
- [ ] Add `BuyItem` and `ConsumeItem` handlers.
- [ ] Reuse the pricing policy and ledger transfer path.
- [ ] Add stock checks that feature 03 can deepen.
- [ ] Apply needs effects through the existing need rules.
- [ ] Add player consumption read fields and admin actions.
- [ ] Add tests for purchase, insufficient funds, empty pantry and need recovery.
- [ ] Assert no unledgered wallet mutation.

## Files

- new `Genelife/Components/Player/HouseholdPantry.cs`
- new `Genelife/Player/PlayerConsumptionService.cs`
- `Genelife/Systems/ConsumerSpendingSystem.cs`
- `Genelife/Components/Gen/Needs.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Player/PlayerConsumptionTests.cs`

## Acceptance

- A player can buy an item only at a reachable store with sufficient funds.
- The purchase debits the wallet, credits the store and records the ledger entry.
- The pantry increments exactly once per purchase.
- A player can consume only an item that exists in the pantry.
- Consumption applies the need effect and emits an event.
- An empty pantry or insufficient funds produces a typed rejection, not a partial state.
- Only the approved creation sources mint money.

## Testing

- Buy food, assert wallet, ledger and pantry.
- Attempt a purchase with insufficient funds and assert no state change.
- Consume food and assert the need change.
- Attempt to consume with an empty pantry and assert rejection.
- Run a needs cycle with no food and assert the same consequences as an NPC.
- Replay a purchase/consume sequence and compare state.

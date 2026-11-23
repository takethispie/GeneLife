# C05 — Shopping trips and nutrition

**Phase:** 5 — Consumption
**Depends:** C04, feature 01 pantry, V1 needs and health
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Connect grocery stock to household consumption: households make shopping trips, fill the pantry, eat, and experience nutrition effects over time.

## Context

Feature 01 adds a player purchase/consume loop with a basic pantry. C04 adds catalog, stock and spoilage. NPC households still spend without a pantry or a nutrition model.

Shopping and nutrition must use the same catalog and stock for players and NPCs, and must not become a second needs system.

## Design

### Shopping trips

- A household with low pantry stock schedules a shopping trip to the nearest grocery store.
- NPC trips are decided by `ConsumerSpendingSystem` using household cash, pantry level and store distance.
- Player trips are explicit `BuyItem` commands from feature 01.
- A trip reserves stock, debits the household and credits the store.
- A trip can partially fill the pantry if funds or stock run out.

### Pantry

Extend the feature 01 `HouseholdPantry` to hold item units by catalog index:

```csharp
public record struct PantryItem
{
    public int ItemIndex;
    public int Units;
    public long AcquiredTick;
}
```

- The pantry has a capacity by unit tier.
- Spoilage removes expired units.
- A household with no pantry space cannot buy more.

### Meals and nutrition

- A household consumes meals from the pantry at meal cadence.
- A meal combines available items; a balanced meal uses at least one staple and one produce/protein.
- `NutritionState.Balance` moves toward balanced or poor based on the meal mix.
- Poor nutrition increases health risk and need decay; balanced nutrition slows it.
- A household with no food experiences hunger and the existing health consequences.

### Player visibility

- The player sees pantry units, spoilage warnings and nutrition state.
- The player can choose cheaper or better items within stock.
- The player cannot eat from another household's pantry.

## Tasks

- [ ] Extend the pantry to item units and capacity.
- [ ] Add NPC shopping trips to `ConsumerSpendingSystem`.
- [ ] Add meal composition and nutrition state.
- [ ] Apply nutrition modifiers to health and needs.
- [ ] Add pantry, spoilage and nutrition read fields.
- [ ] Add player warnings for low food and poor nutrition.
- [ ] Add tests for trips, partial fills, meals and malnutrition.
- [ ] Assert no money or food is created without a source.

## Files

- new `Genelife/Consumption/ShoppingTripService.cs`
- new `Genelife/Consumption/NutritionPolicy.cs`
- `Genelife/Components/Player/HouseholdPantry.cs`
- `Genelife/Systems/ConsumerSpendingSystem.cs`
- `Genelife/Systems/NutritionSystem.cs`
- `Genelife.UnitTests/Consumption/NutritionTests.cs`

## Acceptance

- A household with low stock schedules a trip and buys from available stock.
- A partial fill is possible and leaves the rest unbought.
- The pantry respects capacity and spoilage.
- A balanced meal improves or maintains nutrition; a poor diet degrades it.
- Nutrition affects health and needs without replacing them.
- The player sees pantry, spoilage and nutrition state.
- No food or money is created without a source.

## Testing

- Run a household through several shopping and meal cycles.
- Force a stock-out and assert the trip is partial or rejected.
- Advance shelf life and assert spoilage.
- Feed a poor diet and assert the nutrition decline and health effect.
- Compare player and NPC households under the same conditions.
- Replay a month and compare pantry and nutrition.

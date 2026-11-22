# E11 — Consumer spending

**Phase:** 6 — Demand
**Depends:** E05, E07
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Households spend money on needs-driven categories, constrained by what they can afford. Spending becomes the demand that funds companies.

## Context

`Wallet` exists but nothing spends it. The population only consumes needs through `ActionSystem`, which restores needs for free. That means wages have no purpose and companies have no revenue.

## Design

Spending is **household-level**, not sim-level, so a family pays one rent and one grocery bill. Each household has:

```csharp
public record struct HouseholdSpending
{
    public Money CashAvailable;
    public Money LastCycleSpent;
    public Money LifetimeSpent;
    public Money UnmetDemand;
    public long LastSpendTick;
}
```

Cadence: every 100 ticks (monthly).

Spending basket by stage and archetype:

| Category | Share | Restores | Notes |
|---|---|---|---|
| Food | 45% | Hunger | required every month |
| Hygiene | 15% | Hygiene | soap, utilities |
| Leisure | 20% | Energy/mood (future) | discretionary |
| Health | 10% | Health | skipped when unaffordable |
| Durable | 10% | none | saved purchase, future inventory |

Rules:

- Discretionary categories are cut first when cash is short. Food and hygiene are protected.
- A household with no cash records `UnmetDemand` and does not spend; it must not go negative.
- Savings are tapped only after cash, and only if the household has a savings account (E22).
- If no company can supply a category, the spend is not debited; unmet demand is recorded. Do not destroy money to fake a purchase.
- Company attribution is weighted by employees served and office capacity; use `SimulationRandom`, not `Random.Shared`.

Spending does not bypass needs: it raises a `PurchaseFulfillment` signal that `DecisionSystem` consumes, so an NPC still performs `Eating` but the action is only effective if the household bought food this month.

## Tasks

- [ ] Add `HouseholdSpending` and `HouseholdBasket` configuration.
- [ ] Add `ConsumerSpendingSystem` at order 460, cadence 100.
- [ ] Implement affordability waterfall: cash → savings → cut discretionary → unmet demand.
- [ ] Attribute each purchase to a company and enqueue a `ConsumerSpendBooked` event.
- [ ] Add `PurchaseFulfillment` so free need restoration is removed for non-purchased categories.
- [ ] Emit `UnmetDemandRecorded` for admin metrics.
- [ ] Update `SimStatus`/admin read model with household cash, last spend and unmet demand.
- [ ] Add per-category totals to the economy page.

## Files

- new `Genelife/Components/Household/HouseholdSpending.cs`
- new `Genelife/Systems/ConsumerSpendingSystem.cs`
- new `Genelife/ConsumptionPolicy.cs`
- `Genelife/Systems/DecisionSystem.cs`, `ActionSystem.cs`
- `Genelife.Messages/Events/ConsumerSpendBooked.cs`
- `Genelife.API/Services/SimulationEngine.cs`

## Acceptance

- Households with cash spend each month; spending reduces cash exactly.
- Broke households spend nothing and accumulate unmet demand.
- Food and hygiene are prioritized over leisure and durables.
- Purchases are attributed to existing companies only.
- Total spending equals total company revenue booked for the cycle.
- Needs are not restored for free when the household did not buy the category.

## Testing

- Affordability waterfall test.
- Category priority test.
- Attribution weight distribution test with a seeded random.
- No-company unmet demand test.
- Conservation test against revenue events.

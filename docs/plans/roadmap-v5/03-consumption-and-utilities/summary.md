# Consumption and Utilities — Summary

**Folder:** `03-consumption-and-utilities/`
**Status:** Planned (V5 feature 03)

**Depends on:** V1 housing, rent and consumer spending, V4 households and child costs, feature 01 player consumption, schema 12
**Feeds:** live operations and scale, player life economy, admin consumption page

## Objective

Make household consumption a real constrained system. Water and electricity become explicit networks with production, capacity, load, billing and outages. Grocery becomes a catalog with stock, prices, spoilage and nutrition. The same work recalibrates the economy so `CohabitOnPairing` can default to true without breaking the strict balance bands.

This feature replaces the single opaque utility bill with a service model that players and NPCs can experience, and turns grocery from a minimal spend into a resource the household must manage.

## Baseline before V5

- `RentSystem` bills a single utility amount from `HousingCostPolicy`.
- `Household.UtilityArrears`, `NoUtilities` and `LastUtilityTick` exist, but there is no network, meter, capacity, production or outage.
- Consumer spending exists at household level; grocery is minimal.
- `CohabitOnPairing` is false because enabling it breaks the strict balance bands and worsens the 30-year continuity run.
- V1's 1k–2k balance gaps remain open: vacancy, company failures, wealth Gini and wage share.
- `PersistedWorldState` is schema 12 after feature 01.

## Deliverables

1. Utility domain model: service types, networks, service links, household meters and service levels.
2. Water and electricity networks with production, capacity, load, reserve and outage risk.
3. Metered household billing, arrears, service cut-off and outage effects.
4. Grocery catalog, store inventory, deterministic pricing and spoilage.
5. Shopping trips, pantry integration and nutrition effects.
6. Economy and consumption recalibration that flips `CohabitOnPairing` to true with the strict bands green.
7. Schema 14 persistence, consumption metrics and the admin Consumption page.
8. Conservation, outage, grocery, balance and 30-year continuity acceptance tests.

## Architecture

```
Utility networks (water, electricity)
   │ production + capacity
   ▼
Building service links ──► household meters ──► monthly consumption
                                                   │
                                                   ├── tariff ──► ledger
                                                   ├── arrears ──► service level
                                                   └── outage ──► needs/health
Grocery catalog ──► store inventory ──► shopping trips ──► household pantry
                                                   │
                                                   ├── spoilage
                                                   └── nutrition ──► health/needs
```

Utility networks and grocery stock are indexed by building and household. No household scans the whole world.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Consumption and utility domain model](tech/01-consumption-and-utility-domain-model.md) | Foundation |
| 02 | [Utility networks and capacity](tech/02-utility-networks-and-capacity.md) | Infrastructure |
| 03 | [Household utility billing and outages](tech/03-household-utility-billing-and-outages.md) | Billing |
| 04 | [Grocery catalog, stock and pricing](tech/04-grocery-catalog-stock-and-pricing.md) | Grocery |
| 05 | [Shopping trips and nutrition](tech/05-shopping-trips-and-nutrition.md) | Consumption |
| 06 | [Economy recalibration and cohabitation](tech/06-economy-recalibration-and-cohabitation.md) | Balance |
| 07 | [Persistence, metrics and admin page](tech/07-persistence-metrics-and-admin-page.md) | Durability |
| 08 | [Consumption acceptance](tech/08-consumption-acceptance.md) | Verification |

## Business reading

- [Why utilities and grocery depth](business/01-why-utilities-and-grocery-depth.md)
- [Resource constraints without punishing players](business/02-resource-constraints-without-punishing-players.md)
- [Recalibrating the economy for cohabitation](business/03-recalibrating-the-economy-for-cohabitation.md)

## Definition of done

- Water and electricity are explicit networks with production, capacity, load and outages.
- Every served household has a meter and a monthly bill; every bill is ledgered.
- An unpaid bill leads to arrears, service cut-off and visible need/health consequences.
- Grocery has a catalog, store stock, deterministic prices, spoilage and nutrition.
- Player and NPC households buy from the same catalog and stock.
- Nutrition affects health and needs without creating a second health model.
- `CohabitOnPairing` defaults to true with the strict balance bands and the 30-year continuity run green.
- Schema 12 → 14 migration, restore and double restore are exact.
- The admin Consumption page shows networks, load, outages, meters, stock, prices, spoilage and nutrition.
- Conservation, outage, grocery, balance and 30-year acceptance tests pass.

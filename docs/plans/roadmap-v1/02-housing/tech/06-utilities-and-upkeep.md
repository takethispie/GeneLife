# H06 — Utilities and upkeep

**Phase:** 3 — Tenancy
**Depends:** H05
**Estimate:** 1–2 sessions
**Status:** Planned

## Goal

Separate housing costs into rent, utilities and maintenance so the admin can see where money goes and quality decay has a funding cause.

## Context

Rent alone hides the cost of living. Utilities create a second monthly obligation that can be cut or go unpaid, and maintenance is the reason a landlord spends money on a building.

## Design

### Utilities (tenant)

Monthly bill per unit:

```
utility = baseUtility[tier]
        × occupancyFactor
        × seasonFactor        // 0.9–1.1 deterministic by month
        + luxurySurcharge
```

- Charged in the same `RentSystem` pass as a separate line item.
- Unpaid utilities create `UtilityArrears`, reduce `HousingUnit.Condition` slowly and apply a `NoUtilities` modifier that worsens hygiene and health.
- Relatives and dependents do not pay separately.

### Maintenance (owner/landlord)

Monthly upkeep for the owner:

```
upkeep = property.MonthlyUpkeep × (1 + (1 − condition)) + unitCount × perUnitCost
```

- Company owners debit `CompanyAccount`.
- Household owners debit the owner household.
- Municipality-owned shelter is funded by the government account.
- If upkeep is unpaid, condition decays faster and quality tier effects degrade one step after a threshold.

Condition model:

```
condition -= decayRate × (unpaid ? 2.0 : 1.0) × dtMonths
maintenance payment increases condition toward 1.0 by repairRate
```

Condition is clamped to `[0.1, 1.0]`; a unit below 0.2 becomes uninhabitable and is removed from the market after relocation.

## Tasks

- [ ] Add `UtilityArrears` and `NoUtilities` modifier to `Household`.
- [ ] Extend `RentSystem` to charge utilities and route utility revenue to the utility provider or government.
- [ ] Add `MaintenanceSystem` order 515, cadence 100.
- [ ] Implement condition decay and repair.
- [ ] Add `UtilityBillPaid`, `UtilityArrearsRecorded`, `MaintenancePaid`, `MaintenanceSkipped`, `UnitCondemned`.
- [ ] Expose utility and maintenance totals in the read model.
- [ ] Add admin actions to pay arrears and fund repairs.
- [ ] Add an invariant that condition never leaves its clamp.

## Files

- new `Genelife/Systems/MaintenanceSystem.cs`
- `Genelife/Components/Housing/Household.cs`, `HousingUnit.cs`, `Property.cs`
- `Genelife/Systems/RentSystem.cs`
- new `Genelife/HousingCostPolicy.cs`
- `Genelife.Messages/Events/Utility*.cs`, `Maintenance*.cs`

## Acceptance

- Every occupied unit produces a rent and a utility line each month.
- Unpaid utilities create arrears and a visible quality effect.
- Owners pay upkeep; unpaid upkeep accelerates decay.
- Condition stays in range and condemned units are handled explicitly.
- Admin sees collected rent, utilities, upkeep and arrears separately.
- No cost is charged twice per month.

## Testing

- Utility formula test by tier and occupancy.
- Upkeep funding matrix.
- Condition decay/repair over 24 months.
- Condemnation and relocation test.
- Ledger routing test.

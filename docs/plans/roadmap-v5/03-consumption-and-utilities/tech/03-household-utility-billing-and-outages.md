# C03 — Household utility billing and outages

**Phase:** 3 — Billing
**Depends:** C02, V1 `RentSystem`, `HouseholdFinanceService`, ledger
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Replace the single abstract utility bill with metered water and electricity billing, arrears, service cut-off and visible outage effects.

## Context

`RentSystem` currently computes one utility amount with `HousingCostPolicy.UtilityBill`, charges it through the household finance waterfall, tracks `UtilityArrears` and sets `NoUtilities`. There is no usage, tariff or outage cause.

The existing rent, arrears and eviction behavior must remain intact. Utility billing is a separate obligation with its own service level.

## Design

### Meter reading

- `HouseholdMeter` usage is computed monthly from household size, unit tier, season and service level.
- Usage is capped by the linked network's available service.
- A cut-off household uses zero and accumulates a recovery requirement.

### Tariff and bill

```csharp
public sealed record UtilityTariff
{
    public Money WaterBase;
    public Money WaterPerUnit;
    public Money ElectricityBase;
    public Money ElectricityPerUnit;
    public int ReducedServiceBasisPoints;
}
```

- The bill is `base + usage * rate` per service.
- The household finance waterfall pays cash, then savings, then arrears.
- Every payment is a `UtilityBillPaid` ledger entry to the configured utility settlement account. V5a uses the government account; utility ownership is a later feature.
- A partial payment records `UtilityArrearsRecorded`.
- Unpaid arrears beyond the configured threshold set `ServiceLevel.Reduced`, then `CutOff`.

### Outage

`UtilityOutageSystem` (order 472) runs monthly:

- a network in `Overloaded` or `Offline` can cut service to the lowest-priority links;
- a cut-off household gets a `UtilityServiceCutOff` event with a cause;
- water cut-off affects hygiene and health; electricity cut-off affects needs and food spoilage;
- recovery requires payment, network repair or reconnection;
- outages never delete money.

### Integration with rent

- Rent remains its own bill and eviction path.
- Utility arrears do not directly evict in V5a; they reduce service.
- `Household.NoUtilities` becomes derived from service levels, not a separate flag.
- The old flat utility bill is removed or delegated to the new tariff.

## Tasks

- [ ] Add `UtilityTariff` and the metered bill calculation.
- [ ] Replace the flat utility path in `RentSystem`.
- [ ] Add service levels and the reduced/cut-off ladder.
- [ ] Implement `UtilityOutageSystem` and cause events.
- [ ] Apply water/electricity effects to needs and health.
- [ ] Add recovery, reconnection and arrears payment.
- [ ] Add admin read fields and a manual repair action.
- [ ] Add tests for billing, arrears, cut-off, outage and recovery.

## Files

- new `Genelife/Consumption/UtilityBillingService.cs`
- new `Genelife/Systems/UtilityOutageSystem.cs`
- `Genelife/Systems/RentSystem.cs`
- `Genelife/Housing/HousingCostPolicy.cs`
- `Genelife/Components/Housing/Household.cs`
- `Genelife.UnitTests/Consumption/UtilityBillingTests.cs`

## Acceptance

- A served household receives a metered water and electricity bill.
- The bill is paid through the existing waterfall and ledgered.
- Partial payment creates arrears and a reduced service level.
- Sustained non-payment leads to cut-off with a visible cause.
- An overloaded network can cut the lowest-priority service.
- Recovery restores service and clears the derived flag.
- Rent and eviction behavior remain unchanged.

## Testing

- Bill a household for several months and assert ledger and meter.
- Stop payment and assert reduced then cut-off.
- Overload the network and assert the priority cut.
- Repair and pay arrears; assert recovery.
- Assert no money is created or destroyed.
- Restore and assert meters, arrears and service levels.

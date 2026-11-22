# H15 — Acceptance

**Phase:** 11 — Verification
**Depends:** H01–H14
**Estimate:** 1 session
**Status:** Planned

## Goal

Close the housing feature with an explicit, checkable definition of done.

## Functional checklist

- [ ] Every adult sim belongs to exactly one household; every child belongs to an adult household.
- [ ] Every household has one unit or an explicit sheltered/homeless state.
- [ ] A unit has at most one household.
- [ ] Rent and utilities are charged once per month and route to the correct landlord.
- [ ] Partial payment records exact arrears; full payment clears them.
- [ ] Arrears ladder runs warning → late → notice → eviction with a cure window.
- [ ] Eviction relocates to the best available option before homelessness.
- [ ] Shelter capacity is finite and overflow is explicit.
- [ ] Housing quality changes needs decay, recovery and health drift.
- [ ] Moving is trigger-based, costs money and respects a cooldown.
- [ ] Ownership requires wealth, stops rent and starts maintenance.
- [ ] Construction responds to vacancy and completes over months.
- [ ] Rent index is bounded and existing tenancies adjust at most annually.
- [ ] Save/restore preserves every housing relation and the market state.

## Integration checklist

- [ ] Rent is paid from economy wallets and company accounts.
- [ ] Landlord companies receive rent revenue and pay upkeep.
- [ ] Government funds shelters and municipality construction.
- [ ] Housing modifiers feed the life-stage health and mortality systems.
- [ ] Estate settlement releases or transfers a deceased household's unit.
- [ ] Population growth and immigration are absorbed by assignment and supply.

## Admin checklist

- [ ] Housing page shows stock, occupancy, vacancy, arrears, rent index and homelessness.
- [ ] Property, unit and household lists are filterable and paginated.
- [ ] Create property, set rent, assign, evict and construct all work through commands.
- [ ] Rent roll and arrears ageing are accurate.
- [ ] Health warnings match the metric bands.
- [ ] Non-admin users cannot mutate housing.

## Quality gates

- [ ] `dotnet build` and `dotnet test` green.
- [ ] All five scenarios in H14 pass at 1,000 households.
- [ ] Performance guardrail passes at 2,000 households.
- [ ] No `Random.Shared` in housing systems.
- [ ] No structural mutation inside a query callback.
- [ ] No I/O on the tick thread.
- [ ] Ledger conservation holds after a 10-year housing run.

## Exit criteria

Housing is done when a new population can be spawned, housed, employed, paid, charged rent, shocked by a bankruptcy, evicted, sheltered, rehoused and observed entirely through the admin interface, with every money movement reconciled.

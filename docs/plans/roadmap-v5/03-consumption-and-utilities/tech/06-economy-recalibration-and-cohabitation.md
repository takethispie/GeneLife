# C06 — Economy recalibration and cohabitation

**Phase:** 6 — Balance
**Depends:** C02–C05, V4 `CohabitOnPairing`, `BalanceScenarioTests`
**Estimate:** 3 sessions
**Status:** Planned (V5)

## Goal

Recalibrate household consumption and the demand loop so `CohabitOnPairing` can default to true without breaking the strict balance bands or the 30-year continuity run.

## Context

V4 release evidence records the before/after failure when cohabitation is enabled. The V4 status is explicit that this needs economy/consumption recalibration and seeded-pair cohabitation, not a housing-only tweak.

V5 now has real utility and grocery consumption. That is the lever, but it also changes household budgets, company revenue and vacancy at the same time.

## Design

### Recalibration inputs

- household size and tier;
- rent and utility tariff;
- grocery prices and nutrition;
- seeded-pair cohabitation share;
- construction and housing supply response;
- company revenue and wage share;
- vacancy and overcrowding.

### Seeded-pair cohabitation

- `PopulationGenerator` creates seeded couples as one household when capacity allows.
- Seeded children belong to the couple's household.
- The seed policy is deterministic and recorded in the world options.
- Later pairing uses the same capacity-aware merge.

### Balance procedure

1. Record the V4 baseline with `CohabitOnPairing = false`.
2. Enable metered utilities and grocery consumption with cohabitation still false.
3. Re-run the 10-year balance scenario and the 30-year continuity scenario.
4. Enable seeded-pair cohabitation.
5. Re-run both scenarios and record the deltas.
6. Tune consumption, tariffs, construction and demand within the existing bands.
7. Flip `CohabitOnPairing` only when both scenarios are green.

### Acceptance bands

- Strict wage share, vacancy, company failure and conservation bands remain green.
- The 30-year run has no new `Fail` and no unexplained `Warn` growth.
- Overcrowding, vacancy and health warnings are reported before and after.
- The before/after table is recorded in release evidence.

### What not to do

- Do not mint money to make households solvent.
- Do not relax the bands to make the result pass.
- Do not hide the change behind a player-only exemption.
- Do not flip the default in the same commit that first runs the scenario.

## Tasks

- [ ] Add seeded-pair cohabitation to `PopulationGenerator`.
- [ ] Add the consumption/tariff/grocery calibration options.
- [ ] Record the V4 baseline.
- [ ] Run the scenarios with utilities and grocery, cohabitation off.
- [ ] Run the scenarios with seeded-pair cohabitation on.
- [ ] Tune within the strict bands.
- [ ] Flip `CohabitOnPairing` only after the evidence is recorded.
- [ ] Add a regression test for the default.

## Files

- `Genelife/Family/RelationshipOptions.cs`
- `Genelife.API/Services/PopulationGenerator.cs`
- `Genelife/Housing/HousingCostPolicy.cs`
- `Genelife/Consumption/UtilityTariff.cs`
- `Genelife.UnitTests/Performance/BalanceScenarioTests.cs`
- `Genelife.UnitTests/Performance/ContinuityScenarioTests.cs`
- `docs/plans/roadmap-v5/05-live-operations-and-scale/release-evidence.md`

## Acceptance

- Seeded couples share a household when capacity allows.
- Metered utilities and grocery consumption run for 10 and 30 years.
- The strict balance bands remain green.
- The 30-year continuity run has no new `Fail`.
- `CohabitOnPairing` defaults to true only after the evidence is recorded.
- The before/after numbers are reproducible from the same seed and options.

## Testing

- 10-year balance scenario with cohabitation off and on.
- 30-year continuity scenario with cohabitation off and on.
- 1k and 2k population runs.
- Conservation and money-growth assertions.
- Restore and replay the tuned world.

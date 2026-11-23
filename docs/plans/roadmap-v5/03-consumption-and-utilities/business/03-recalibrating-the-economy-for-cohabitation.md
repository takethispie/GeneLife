# Recalibrating the economy for cohabitation

`CohabitOnPairing` is the last open V4 item. Enabling it currently breaks the strict balance bands:

- wage share 0.9078 → 1.0527;
- vacancy 0.0854 → 0.2152;
- 30-year overcrowded 1 → 12;
- vacancy 20.12% → 31.65%;
- health warnings 4 → 27.

The V4 release notes are explicit: this needs economy and consumption recalibration plus seeded-pair cohabitation, not a housing-only tweak. V5 feature 03 is where that recalibration happens.

## Why cohabitation changes the economy

- Two adults in one unit pay one rent instead of two, so household disposable income rises.
- Utility and grocery consumption per household changes with household size.
- Fewer occupied units raise vacancy and reduce landlord revenue.
- Crowding affects health and housing demand.
- Couples formed at seed time behave differently from couples formed later.

## What the recalibration must cover

1. **Household consumption.** Rent, utilities, food and goods scale with household size and tier, not a flat per-sim value.
2. **Wage share.** The demand and spending loop must absorb the extra disposable income without pushing wage share past its ceiling.
3. **Vacancy.** Seeded-pair cohabitation and later pairing must not leave a wave of empty units.
4. **Health and crowding.** The overcrowding penalty must be calibrated so cohabitation does not create a health crisis.
5. **Construction.** Housing supply must respond to the new demand pattern.
6. **Company revenue.** Grocery and utility revenue must be funded by real household spending.

## Acceptance bands

- The strict balance bands in `BalanceScenarioTests` remain green.
- The 30-year continuity run has no new `Fail` and no unexplained `Warn` growth.
- Overcrowding, vacancy and health warnings are reported before and after.
- The change is recorded in the release evidence with the before/after numbers.
- `CohabitOnPairing` flips to true only after the evidence is recorded.

## What not to do

- Do not hide the change behind a player-only exemption.
- Do not mint money to make households solvent.
- Do not lower the balance bands to make the result pass.
- Do not flip the default in the same commit that first runs the scenario.

## Success signal

A seeded world with cohabiting couples, metered utilities and grocery consumption runs for 30 years inside the same strict bands as V4, and the default can change without a silent regression.

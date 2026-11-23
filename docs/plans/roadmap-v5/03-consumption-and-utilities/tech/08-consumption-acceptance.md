# C08 — Consumption acceptance

**Phase:** 8 — Verification
**Depends:** C01–C07
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Prove that utilities, grocery and nutrition are conserved, deterministic, durable and balanced before V5 release.

## Acceptance scenarios

### Utility networks

1. Bootstrap a world and assert one water and one electricity network.
2. Add served buildings and assert load equals linked demand.
3. Overload a network and assert the configured priority reduction.
4. Expand capacity and assert recovery.
5. Remove a link and assert the building is unserved.

### Billing and outages

1. Bill a household for several months and assert ledger entries.
2. Stop payment and assert arrears, reduced service and cut-off.
3. Cut off water and electricity and assert need/health effects.
4. Pay arrears and repair the network; assert recovery.
5. Assert no money is created or destroyed.

### Grocery

1. Buy an item and assert stock, wallet and ledger.
2. Sell out and assert rejection.
3. Restock and assert stock and supplier cost.
4. Advance shelf life and assert spoilage.
5. Assert prices are deterministic and clamped.

### Nutrition

1. Feed a balanced diet and assert stable/improving nutrition.
2. Feed a poor diet and assert decline and health effect.
3. Assert a household with no food experiences hunger.
4. Assert nutrition does not replace the existing health model.

### Balance

1. Run the 10-year balance scenario with cohabitation off.
2. Run it with seeded-pair cohabitation on.
3. Run the 30-year continuity scenario both ways.
4. Assert the strict bands and no new `Fail`.
5. Assert `CohabitOnPairing` defaults to true only after the evidence.

### Migration and replay

1. Load a schema 13 fixture.
2. Migrate to 14 and assert networks, links, meters, stock and nutrition.
3. Restore again and compare.
4. Replay a year and compare consumption.
5. Assert a legacy household is not silently cut off.

### Admin

1. Open `/consumption` with empty, seeded and migrated worlds.
2. Inspect networks, meters, outages, stock and nutrition.
3. Run a manual repair and restock; assert audit.
4. Assert unauthorized access is rejected.

## Tasks

- [ ] Add the consumption acceptance suite.
- [ ] Add the schema 13 fixture.
- [ ] Add the balance before/after report.
- [ ] Add the 30-year cohabitation report.
- [ ] Add the admin smoke test.
- [ ] Record results in `roadmap-v5/STATUS.md` when implemented.
- [ ] Update the release evidence with the final numbers.

## Files

- `Genelife.UnitTests/Consumption/ConsumptionAcceptanceTests.cs`
- `Genelife.UnitTests/Fixtures/v13-consumption.json`
- `Genelife.UnitTests/Performance/ConsumptionBalanceTests.cs`
- `docs/plans/roadmap-v5/05-live-operations-and-scale/release-evidence.md`
- `docs/plans/roadmap-v5/STATUS.md`

## Acceptance

- Every scenario passes on a clean build.
- Utilities and grocery conserve money and goods.
- Outages, spoilage and nutrition are visible and reversible through play.
- The strict balance bands and 30-year run stay green with cohabitation on.
- Schema 13 → 14 migration and double restore are exact.
- No existing economy, housing or family test regresses.

## Testing

- Full consumption suite.
- Economy, housing, family and persistence suites.
- 10-year and 30-year scenarios with cohabitation off and on.
- 2,000-sim decade with utilities and grocery.
- Admin HTTP smoke test.

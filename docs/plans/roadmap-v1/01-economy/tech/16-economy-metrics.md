# E16 — Economy metrics

**Phase:** 9 — Observability
**Depends:** E06, E12, E15
**Estimate:** 1–2 sessions
**Status:** Implemented (V1)

## Goal

Capture a yearly picture of the economy so balance regressions are visible in the admin instead of hidden in logs.

## Context

`Metrics` already records per-system time and allocations. There is no economic telemetry and no way to answer "is the economy healthy after ten years?".

## Design

`EconomyMetricsSystem` order 610, cadence 1,200 (yearly). It reads, never mutates.

```csharp
public sealed record EconomySnapshot(
    long Tick,
    int Year,
    Money HouseholdCash,
    Money HouseholdSavings,
    Money CompanyCash,
    Money CompanyDebt,
    Money PensionFund,
    Money GovernmentFund,
    Money TotalMoneySupply,
    Money WagesPaidThisYear,
    Money RevenueThisYear,
    Money TaxThisYear,
    Money Arrears,
    decimal UnemploymentRate,
    decimal VacancyRate,
    decimal WageShare,
    decimal ProfitShare,
    decimal Gini,
    int HomelessHouseholds,
    int BankruptCompanies);
```

Computations:

- **Money supply** = all wallets + savings + company balances + pension fund + government account.
- **Unemployment** = job seekers without `Employee` / working-age population.
- **Vacancy** = open seats / (open seats + employed).
- **Wage share** = wages / revenue for the year.
- **Gini** over household total wealth; use the sorted-wallet formula, O(n log n).
- **Homelessness** from `HousingRegistry` (housing feature).

Storage: in-memory ring buffer of the last 50 years and a `EconomySnapshot` event persisted to Marten. Endpoints:

- `GET /api/economy/metrics/current`
- `GET /api/economy/metrics/history?years=20`
- `GET /api/economy/health` — pass/warn/fail against the target bands in `business/04-balance-targets.md`.

Health checks run at the same cadence:

| Check | Warn | Fail |
|---|---|---|
| Money supply growth per year | > 15% | > 30% or negative |
| Unemployment | > 10% | > 20% |
| Wage share | < 40% or > 80% | < 30% |
| Arrears share of payroll | > 10% | > 25% |
| Homeless households | > 5% | > 10% |
| Solvent companies | < 2 | 0 |

## Tasks

- [ ] Add `EconomySnapshot`, ring buffer and health evaluator.
- [ ] Implement `EconomyMetricsSystem` with allocation-light aggregation.
- [ ] Persist snapshots through `OutboundEvents` and a Marten projection.
- [ ] Add endpoints for current, history and health.
- [ ] Add charts/tables to the economy admin page.
- [ ] Add a CI scenario test asserting the target bands after a 10-year run.
- [ ] Log a single structured warning per failing check, not per entity.

## Files

- new `Genelife.API/Services/EconomyMetrics.cs`
- new `Genelife/Systems/EconomyMetricsSystem.cs`
- `Genelife.Messages/Events/EconomySnapshotTaken.cs`
- new `Genelife.API/Models/EconomySnapshotDocument.cs`
- `Genelife.API/Endpoints/Wolverine/EconomyEndpoints.cs`
- `Genelife.Web/Pages/Economy.razor`

## Acceptance

- One snapshot per simulated year with all fields populated.
- Money supply reconciles with the ledger within rounding.
- Health endpoint reports the expected state for known good and bad scenarios.
- Admin can compare years and see failing indicators.
- Metrics collection does not allocate per entity or write to the DB on the tick thread.

## Testing

- Snapshot aggregation test with a fixed world.
- Gini edge cases (zero wealth, one household, equal wealth).
- Health threshold table test.
- 10-year scenario guardrail test.

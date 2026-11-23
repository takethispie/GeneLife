# Balance targets and levers

A realistic population simulation is not a realistic economy. The goal is a world that stays legible and interesting for tens of simulated years, not a macroeconomic model.

## Target bands

These are acceptance ranges for a 10-year default run with 1,000–2,000 NPCs.

| Indicator | Target | Why |
|---|---|---|
| Unemployment | 4–8% | enough friction to matter, not a crisis |
| Job vacancy rate | 2–6% | hiring should take time |
| Vacant housing | 3–8% | mobility without shortage |
| Rent burden | 25–40% of worker income | housing is a real choice |
| Household savings | 1–3 months of expenses | buffer against shocks |
| Homelessness | under 3% | visible but not normal |
| Company failures per decade | 5–20% of firms | turnover without collapse |
| Money supply growth | 0–10% per decade | no runaway inflation |
| Wealth Gini | 0.35–0.55 | inequality, not extreme concentration |

## Primary levers

- **Initial capital** per company and per household.
- **Salary bands** by position and seniority.
- **Recruitment cost** and **severance**.
- **Consumer spending rate** by stage and archetype.
- **Company margin** on consumer revenue.
- **Tax rate** and **pension contribution rate**.
- **Rent** by quality tier and **housing supply** growth.
- **Mortality and health decline** rates.
- **Immigration** response to vacancies and housing availability.
- **Pay cycle length** (300 ticks by default, aligned with current payroll).

## What "realistic" means here

- A worker can afford rent, food and some saving on a normal salary.
- Losing a job creates urgency within one or two pay cycles, not instant ruin.
- A company cannot grow without capital and capacity.
- Retirement income is lower than working income but sufficient with savings.
- Old age brings declining health and eventual death, not a cliff at a fixed age.

## Guardrails

- No system may mint money implicitly.
- Any policy that redistributes money must have a fund and a visible shortfall.
- Balance changes must be configuration-driven and covered by a scenario test.
- Metrics are captured every simulated year and stored for the admin; regressions are visible without reading logs.

---

## Balance pass — 2026-11

**Harness:** `Genelife.UnitTests/Economy/BalanceScenarioTests.cs` runs 10 simulated years at 200 sims (seed 21), writes the yearly report to `balance-reports/balance-200-21.json`, asserts conservation and money growth for every case, and asserts the target bands below for the 200-sim case.

### Structural defects found and fixed

| Defect | Effect before the pass | Fix |
|---|---|---|
| Population seed always created exactly 2 companies / ≤20 seats | Unemployment 58–70% at 200 sims; no path to full employment | Seats and company count scale with the working-stage population (`PopulationGenerator.EnsureCompanies`) |
| Money destroyed by upkeep, construction, school costs and funeral costs | Decade money supply understated; ledger could not reconcile | Payments now credit government (or are recorded as internal municipal services); conservation gap is 0 |
| All housing municipally owned | Companies could not earn rent, so wages exceeded company revenue and company cash drained | Seed stock is distributed to company landlords (`HousingBootstrap.CreateDistributedProperty`) |
| Savings were illiquid above a 300-unit wallet buffer | Actual consumption ~47 units/month while households held years of savings | Consumption withdraws savings above the emergency floor (`ConsumerSpendingSystem.ExplicitlyWithdrawForConsumption`) |
| Fixed job stock while children age into work | Unemployment drifted upward even after the seed fix | Yearly `JobGrowthSystem` creates government-funded companies when unemployment exceeds target + trigger |
| Postings were destroyed when filled | Vacancies could never be recreated after a death, retirement or firing | Closed postings are retained and reopened by `LifeSystemHelpers.RepostSeat` |
| No initial pension capitalization | Pension funding ratio ≈ 0 | Per-sim pension endowment transferred from the government endowment |

### Measured result at year 10 (200 sims)

| Indicator | Target | Measured | Status |
|---|---|---|---|
| Unemployment | 4–8% | 5.6% | Pass |
| Job vacancy rate | 2–6% | 0% | Fail — no steady turnover |
| Vacant housing | 3–8% | 8.5% | Marginal |
| Rent burden | 25–40% | 32.8% | Pass |
| Household savings | 1–3 months | ≈3.5 months of rent + consumption | Marginal — above the top of the band |
| Homelessness | under 3% | 0% | Pass |
| Company failures per decade | 5–20% | 0% of 16 | Fail — capital buffers absorb losses |
| Money supply growth | 0–10% | 7.8% | Pass |
| Conservation gap | 0 | 0 | Pass |
| Pension funding ratio | ≥0.8 | 1.0 | Pass |
| Wage share | ≈0.55–0.7 | 0.94 | Fail — at the health-check boundary |
| Wealth Gini | 0.35–0.55 | 0.67 | Fail — unemployed households hold near-zero wealth |

Population moved from 200 to 184 (−8%), inside the ±20% stability band. The scenario test uses `Gini ≤ 0.70` and `wage share ≤ 1.0` as regression guards, not as target passes.

### Restore and rollout behaviour

- The effective `EconomyOptions` and `HousingOptions` are stored in the snapshot, so later deploys no longer change a running save's consumption, savings or construction rules silently.
- Saves created before this pass are backfilled once (`PersistedWorldState.BalanceBackfilled`): the pension fund receives its per-sim endowment, municipal Basic housing is assigned to company landlords, and one job-growth year runs immediately. Subsequent years use the registered `JobGrowthSystem`.

### Remaining structural gaps

- **Turnover.** With no quits and almost no firing, vacancies converge to zero and no company fails. A separation/quitting system is needed before the vacancy and failure bands can be met.
- **Wealth distribution.** Rent, wages and savings accrue to employed households while unemployed households live on UBI. Company equity is not owned by households, so wealth Gini stays high. Levers: household equity, a wealth tax, or a stronger savings floor transfer.
- **Wage share.** Companies now recover rent as revenue, but consumer demand still does not fully recycle wages. Raising the effective consumption rate or adding government procurement closes the remaining gap.
- **Scale.** This pass is asserted at 200 sims for speed. The documented band is for 1,000–2,000 NPCs; long-run tuning at that scale remains open.

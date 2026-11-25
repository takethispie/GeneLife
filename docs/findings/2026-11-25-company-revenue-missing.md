# Why companies show no revenue

Date: 2026-11-25
Status: Root cause identified, not yet fixed

## Symptom

The company table on the Economy page (`Genelife.Web/Pages/Economy.razor:144`) renders
`company.RevenueLastCycle`, and that value is `0` for every company. The same field is
`0` from the REST endpoints that expose `CompanyStatus.RevenueLastCycle`
(`Genelife.Contracts/Dtos/CompanyStatus.cs:12`).

The economy-level counter `EconomyStatus.RevenueThisYear` is not zero: consumer spending
does book revenue and companies do receive cash. Only the per-company "last cycle"
metric is unobservable.

## Root cause 1 (primary): revenue is cleared in the same tick it is recorded

The simulation runs systems in ascending `Order` (`Genelife.Domain/Systems/SystemRegistry.cs:51`).
Both `CompanyRevenueSystem` and `CorporateTaxSystem` use the default pay-cycle cadence
(`economy.Clock.TicksPerPayCycle`, 300 ticks) with no phase offset, so they run on the
same tick.

Execution order on every pay-cycle boundary (300, 600, 900, ...):

| Order | System | What it does to `CompanyAccount.RevenueLastCycle` |
|------:|--------|---------------------------------------------------|
| 450 | `CompanyRevenueSystem` | Credits cash and does `account.RevenueLastCycle += revenue.Total` (`Genelife.Domain/Systems/CompanyRevenueSystem.cs:65`) |
| 460 | `ConsumerSpendingSystem` | Books the next cycle's demand into `PendingRevenue` (`Genelife.Domain/Systems/ConsumerSpendingSystem.cs:215`); does not touch `RevenueLastCycle` |
| 500 | `PayrollSystem` | Accumulates `WagesLastCycle` |
| 510 | `CompanyCostsSystem` | Accumulates `CostsLastCycle` |
| 535 | `CorporateTaxSystem` | Computes profit from the cycle fields, then sets `live.RevenueLastCycle = Money.Zero` (and zeroes wages, costs and interest) at `Genelife.Domain/Systems/CorporateTaxSystem.cs:121` |

By the time the tick finishes, `RevenueLastCycle` has been set and then reset in the same
tick. Nothing can observe the non-zero value:

- `SimulationEngine.Tick` runs `systems.Update(...)` first and only then publishes the
  snapshot (`Genelife.Application/Services/SimulationEngine.cs:610-660`).
- `PublishSnapshot` builds `CompanyStatus` from `CompanyAccount` after all systems ran
  (`Genelife.Application/Services/SimulationEngine.cs:6510-6555`).
- API reads take the read lock, so they can never observe a mid-tick state.
- `/api/data` (used by the Economy page) goes through `SimulationManager.GetWorldData`
  -> `SimulationEngine.GetWorldData` -> the latest snapshot (`SimulationEngine.cs:3400-3450`).
- `/api/economy/companies` calls `engine.GetWorldData().Companies` directly
  (`Genelife.API/Endpoints/Wolverine/EconomyEndpoints.cs:47-50`).
- `CompanyRepository.Get`/`GetAll` read `account.RevenueLastCycle` directly
  (`Genelife.Application/Repositories/CompanyRepository.cs:116,146`) and are also only
  reachable between ticks, so they see the same zero.

The field is being used for two incompatible purposes:

1. a per-cycle accumulator that `CorporateTaxSystem` consumes and must reset, and
2. the "last completed cycle" value that the UI and API display.

Because there is only one field, the reset for (1) destroys (2).

`WagesLastCycle`, `CostsLastCycle` and `InterestPaidLastCycle` are zeroed by the same
block (`CorporateTaxSystem.cs:121-124`), so the "Wages" column is affected as well even
though the report focuses on revenue.

### Execution timeline (default clock: month = 100, pay cycle = 300)

- Tick 100: `ConsumerSpendingSystem` books revenue into `PendingRevenue`.
- Tick 200: `ConsumerSpendingSystem` books more revenue into `PendingRevenue`.
- Tick 300:
  - `CompanyRevenueSystem` settles pending revenue into cash and `RevenueLastCycle`.
  - `ConsumerSpendingSystem` books tick-300 demand for the next cycle.
  - `CorporateTaxSystem` reads the cycle fields for tax and clears them.
  - Snapshot is published. `RevenueLastCycle == 0`.

This repeats every 300 ticks, so every published snapshot and every REST response shows
zero per-company revenue.

## Root cause 2 (independent): the Marten read model drops financial fields

`/api/companies` and `/api/companies/{networkId}` prefer the Marten read model
(`Genelife.Application/Services/SimulationManager.cs:216-244`). `MartenReadModelStore`
returns `CompanyDocument.ToCompanyStatus()`, and that projection only copies `NetworkId`,
`Name` and `EmployeeCount` (`Genelife.Infrastructure/Models/CompanyDocument.cs:14-22`).
`CompanyDocument` itself has no balance, revenue, wages, arrears or solvency fields.

Consequences when the read model is active (it is registered by default in
`Genelife.API/Program.cs:101`):

- `/api/companies` returns `RevenueLastCycle = 0`, `Balance = 0`, `Arrears = 0`,
  `Solvency = ""` regardless of simulation state.
- `SimulationManager.GetCompanyAsync` returns that same sparse document instead of the
  richer snapshot.

This is a second, separate reason the revenue appears missing on that API path. Fixing
only the simulation ordering will not fix `/api/companies` while the read model is
enabled.

## What is not broken

- Money is not lost. `CompanyRevenueSystem` credits `CompanyAccount.Balance`
  (`CompanyRevenueSystem.cs:53-66`), and the closed-loop tests still pass.
- `EconomyContext.RevenueThisYear` accumulates at settlement
  (`CompanyRevenueSystem.cs:87`) and is exposed by `EconomyStatus.RevenueThisYear`, so the
  aggregate revenue figure on the Economy page is populated.
- Revenue persistence round-trips correctly: `RevenueLastCycleCents` is captured at
  `SimulationEngine.cs:6975` and restored at `SimulationEngine.cs:7991`.

## Why this was not caught

- No test asserts `RevenueLastCycle` after a full system pass. The existing test
  `ConsumerSpending_DebitsOnceAndRevenueSettlesToCompany`
  (`Genelife.Domain.Tests/Economy/EconomySystemTests.cs:87-109`) asserts the company
  balance and pending-revenue cleanup, not the last-cycle revenue field.
- The scenario test `ClosedLoop_TenYearRunOnlyAddsBasicIncome`
  (`EconomySystemTests.cs:158-209`) checks money supply, which is unaffected.
- The read-model integration tests only assert that a `CompanyDocument` exists
  (`Genelife.Api.IntegrationTests/Marten/ProjectionIntegrationTests.cs:39-49`), not its
  financial contents.

## Fix direction (for the plan)

The fix needs to separate the tax accumulator from the display value, for example:

- Keep dedicated per-cycle accumulator fields (or reset them explicitly after tax) and
  store the consumed values into separate `LastCycle` display fields, or
- Snapshot revenue/wages/costs/interest before `CorporateTaxSystem` clears them and have
  `CompanyStatus` read the snapshot values, or
- Have `CorporateTaxSystem` move the current-cycle values into display fields before
  zeroing the accumulators.

The read-model path also needs the financial fields added to `CompanyDocument`, populated
by `WorldSaveService.Build` and the company projection (or the endpoints must stop
preferring the sparse read model for financial data).

Either way, add a regression test that runs the real `SystemRegistry` across a pay-cycle
boundary and asserts that published `CompanyStatus.RevenueLastCycle` is non-zero after
settlement.

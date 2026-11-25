# Company Revenue Visibility Implementation Plan

## Context

Two independent root causes make per-company revenue appear as zero:

1. **Same-tick reset.** `CompanyRevenueSystem` (order 450) adds settled revenue to `CompanyAccount.RevenueLastCycle`. Later in the same tick, `CorporateTaxSystem` (order 535) reads that value as a tax accumulator and then sets it to `Money.Zero` at `Genelife.Domain/Systems/CorporateTaxSystem.cs:121-124`. Snapshots and API reads only observe the world between ticks, so the non-zero value is never published. `WagesLastCycle`, `CostsLastCycle`, and `InterestPaidLastCycle` are reset by the same block.
2. **Sparse Marten read model.** `/api/companies` and `/api/companies/{networkId}` prefer `MartenReadModelStore`, whose `CompanyDocument.ToCompanyStatus()` copies only `NetworkId`, `Name`, and `EmployeeCount`. `CompanyDocument` has no balance, revenue, wages, arrears, solvency, or office fields, so that path always returns zeros or empty strings even when the simulation state is correct.

The fix keeps the existing system order and cadence and separates the two roles of the cycle fields:

- `RevenueThisCycle`, `WagesThisCycle`, `CostsThisCycle`, `InterestPaidThisCycle` become the per-cycle accumulators consumed by corporate tax.
- The existing `RevenueLastCycle`, `WagesLastCycle`, `CostsLastCycle`, `InterestPaidLastCycle` fields remain the published display values for the last completed cycle.
- `CorporateTaxSystem` computes profit from the accumulators, copies them into the display fields, and then zeroes only the accumulators.
- The Marten read model is enriched with the same financial fields that `CompanyStatus` exposes.

No DTO shape change is required. No system ordering or cadence change is required.

## Implementation Plan

1. Add the per-cycle accumulator fields to `CompanyAccount` in `Genelife.Domain/Components/Company/CompanyAccount.cs`. Add `RevenueThisCycle`, `WagesThisCycle`, `CostsThisCycle`, and `InterestPaidThisCycle` as `Money` fields alongside the existing `*LastCycle` display fields. Keep `CompanyAccount.Create` unchanged; new fields default to `Money.Zero`.

2. Route all revenue accrual into the new accumulator. In `Genelife.Domain/Systems/CompanyRevenueSystem.cs`, change `CompanyRevenueSystem.Update(long)` to increment `account.RevenueThisCycle` instead of `account.RevenueLastCycle`. In `Genelife.Domain/Economy/CompanyAccountService.cs`, change `CompanyAccountService.ApplySettlement(Entity, Money, long, bool revenue)` to increment `account.RevenueThisCycle` when `revenue` is true. This covers consumer spending plus the rent, housing, grocery, estate, and player-action settlement callers that use `ApplySettlement`.

3. Route payroll accrual into the accumulators. In `Genelife.Domain/Systems/PayrollSystem.cs`, change `PayrollSystem.Update(in SimulationContext)` near the final account update so it increments `account.WagesThisCycle` with `grossTotal` and `account.CostsThisCycle` with `employerPensionTotal`.

4. Route operating-cost accrual into the accumulator. In `Genelife.Domain/Systems/CompanyCostsSystem.cs`, change `CompanyCostsSystem.Update(long)` so the per-company office cost total is added to `account.CostsThisCycle` instead of `account.CostsLastCycle`.

5. Route interest accrual and keep the loan limit on display revenue. In `Genelife.Domain/Systems/CreditSystem.cs`, change `CreditSystem.ServiceLoans(long)` so paid interest increments `account.InterestPaidThisCycle`. Leave `CreditSystem.IssueLoan(Entity, Money, long)` using `account.RevenueLastCycle` for `revenueLimit`; after this change that field is the last completed cycle's revenue rather than a same-tick-zeroed accumulator.

6. Roll accumulators into display values in `Genelife.Domain/Systems/CorporateTaxSystem.cs` `CorporateTaxSystem.Update(long)`. Compute `profit` from `work.RevenueThisCycle - work.WagesThisCycle - work.CostsThisCycle - work.InterestPaidThisCycle`. Before clearing, assign `work.RevenueLastCycle`, `work.WagesLastCycle`, `work.CostsLastCycle`, and `work.InterestPaidLastCycle` from the corresponding `*ThisCycle` values, then zero only the four `*ThisCycle` accumulators on `live`. Keep the existing `LastProfit`, `LastTaxPaid`, loss carry-forward, arrears, tax payment, and bankrupt-company skip logic unchanged so the tax base is exactly the same numbers that are published.

7. Audit the direct API and UI mappings so every read uses the display fields. Confirm `SimulationEngine.BuildCompanySnapshots(World, EmploymentIndex)`, `SimulationEngine.GetWorldData()`, `CompanyRepository.Get(NetworkId)`, and `CompanyRepository.GetAll()` in `Genelife.Application/Repositories/CompanyRepository.cs` continue to read `RevenueLastCycle`, `WagesLastCycle`, `Balance`, `ReservedForPayroll`, `Arrears`, and `Solvency` from `CompanyAccount` display values, never from `*ThisCycle`. Confirm `Genelife.Web/Pages/Economy.razor` still binds the company table to `RevenueLastCycle` and `WagesLastCycle`. Do not add new `CompanyStatus` fields.

8. Add financial fields to the Marten read model in `Genelife.Infrastructure/Models/CompanyDocument.cs`. Add `decimal Balance`, `decimal ReservedForPayroll`, `decimal Arrears`, `decimal RevenueLastCycle`, `decimal WagesLastCycle`, `string Solvency`, and `int OfficeCount`. Update `CompanyDocument.ToCompanyStatus()` to copy every one of those fields, plus the existing `NetworkId`, `Name`, and `EmployeeCount`, into `CompanyStatus`.

9. Populate the new read-model fields in `Genelife.Infrastructure/Services/WorldSaveService.cs` `WorldSaveService.Build(World, EmploymentIndex)`. During the company query, read the `CompanyAccount` when present and set `Balance`, `ReservedForPayroll`, `Arrears`, `RevenueLastCycle`, `WagesLastCycle`, and `Solvency` from the account. Count open offices per company with one office pass, as `SimulationEngine.BuildCompanySnapshots` does, and set `OfficeCount`. Leave the fields at their defaults when a company has no `CompanyAccount`.

10. Make the event projection non-destructive for financial fields in `Genelife.Infrastructure/Projections/CompanyProjection.cs`. Change `CompanyProjection.Create(CompanyCreated)` so an existing `CompanyDocument` with the same `DocumentIds.ForNetworkId(e.NetworkId)` is updated in place rather than replaced with a zero-filled document; when no document exists, create one with the existing identity and employee defaults. Keep `Project(EmployeeHired, ...)` and `Project(EmployeeFired, ...)` loading and mutating only `EmployeeCount` and `UpdatedAt`, so periodic `WorldSaveService` financial values are preserved.

11. Persist the new accumulators. Add `RevenueThisCycleCents`, `WagesThisCycleCents`, `CostsThisCycleCents`, and `InterestPaidThisCycleCents` to `PersistedCompany` in `Genelife.Application/Persistence/PersistedWorldState.cs`. Populate them in `SimulationEngine.CaptureState(NetworkIdRegistry)` next to the existing `*LastCycleCents` assignments. Restore them in `SimulationEngine.Restore(PersistedWorldState, NetworkIdRegistry)` next to the existing `*LastCycle` assignments. Bump `PersistedWorldState.CurrentSchemaVersion` to `16` and add a `fromVersion <= 15` migration in `SnapshotMigrations.Upgrade` that seeds the new `*ThisCycleCents` fields from the existing `*LastCycleCents` fields, so a pre-deploy save's in-flight cycle is still published and taxed correctly.

12. Add the required regression test to `Genelife.Domain.Tests/Economy/EconomySystemTests.cs`. Add a test that creates a company with a `CompanyAccount`, an `Office`, an `Employee`, and a buyer `Wallet`, builds a real `SystemRegistry`, registers `CompanyRevenueSystem`, `ConsumerSpendingSystem`, `PayrollSystem`, `CompanyCostsSystem`, and `CorporateTaxSystem`, and calls `registry.Update(new SimulationContext(...))` for every tick through `economy.Clock.TicksPerPayCycle`. After the boundary tick, assert that `CompanyAccount.RevenueLastCycle` is positive, `WagesLastCycle` is positive, all four `*ThisCycle` accumulators are `Money.Zero`, and `LastProfit` equals `RevenueLastCycle - WagesLastCycle - CostsLastCycle - InterestPaidLastCycle`.

13. Add a read-model population test in `Genelife.Domain.Tests/Services/WorldSaveServiceTests.cs`. Create a world with a company, `CompanyAccount` financial values, and open `Office` entities; call `new WorldSaveService().Build(world, employmentIndex)`; assert the returned `CompanyDocument` carries the expected `Balance`, `ReservedForPayroll`, `Arrears`, `RevenueLastCycle`, `WagesLastCycle`, `Solvency`, and `OfficeCount`, and that `ToCompanyStatus()` maps them.

14. Add a Marten round-trip test in `Genelife.Api.IntegrationTests/Marten/DocumentStoreTests.cs`. Store a `CompanyDocument` with non-zero financial fields, reload it from a new session, and assert both the document values and the `ToCompanyStatus()` projection values survive the round trip. This verifies the read-model path that `SimulationManager.GetAllCompaniesAsync()` and `SimulationManager.GetCompanyAsync()` prefer.

## Definition of Done

- After a pay-cycle boundary, `CompanyAccount.RevenueLastCycle` and `CompanyAccount.WagesLastCycle` are non-zero for companies with settled revenue and paid wages, while all four `*ThisCycle` accumulators are zero.
- `CorporateTaxSystem` computes the same profit and tax as before: profit is derived from the same revenue, wages, costs, and interest values that are copied to the display fields.
- `CompanyStatus.RevenueLastCycle` and `CompanyStatus.WagesLastCycle` are non-zero from the snapshot path used by `/api/data` and `/api/economy/companies`.
- `/api/companies` and `/api/companies/{networkId}` return the financial fields from the Marten read model after the periodic world save, with `Balance`, `ReservedForPayroll`, `Arrears`, `RevenueLastCycle`, `WagesLastCycle`, `Solvency`, and `OfficeCount` populated.
- `CaptureState` and `Restore` round-trip both the display fields and the new accumulators.
- The `SystemRegistry` regression test fails against the old reset behavior and passes after the fix.
- `Genelife.Web/Pages/Economy.razor` requires no field changes; the Revenue and Wages columns display the published values.
- No system `Order`, `Cadence`, `CompanyStatus` shape, or API route changes are introduced.

## Validation

Run from the repository root `/Users/dustcat/Documents/repositories/GeneLife`.

Build the solution:

```bash
dotnet build GeneLife.sln
```

Run the new economy regression and the existing economy suite:

```bash
dotnet test Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --filter "FullyQualifiedName~EconomySystemTests"
```

Run the read-model population test:

```bash
dotnet test Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --filter "FullyQualifiedName~WorldSaveServiceTests"
```

Run the full fast suite:

```bash
dotnet test Genelife.Domain.Tests/Genelife.Domain.Tests.csproj
```

Start PostgreSQL, then run the Marten integration test:

```bash
docker compose up -d postgres
dotnet test Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj --filter "FullyQualifiedName~DocumentStoreTests"
```

Run the scenario tier, which includes the existing ten-year closed-loop test:

```bash
dotnet test Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --filter "Category=Scenario"
```

The domain and integration projects use xunit v3 with Microsoft.Testing.Platform. If the installed SDK does not route the `dotnet test` filter syntax to that runner, the repository-native equivalent commands are:

```bash
dotnet build Genelife.Domain.Tests/Genelife.Domain.Tests.csproj
dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-build --no-launch-profile -- -trait- "Category=Scenario"

dotnet build Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj
dotnet run --project Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj --no-build --no-launch-profile --
```

Validation is successful when the solution build has zero errors, the targeted tests pass, the full fast suite passes, and the integration suite passes against the running PostgreSQL instance.
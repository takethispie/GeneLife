# F07 — Child costs and household finance

**Phase:** 7 — Economy
**Depends:** F04–F06, `HouseholdFinanceService`, `ConsumerSpendingSystem`, `School`, `HealthSystem`
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Price children through the existing money loop so a larger family has a real but survivable budget impact, with no minting and no child-specific shadow economy.

## Context

- Each sim has a wallet and consumption needs.
- A newborn starts with zero money, so it cannot pay for its own consumption.
- `HouseholdFinanceService` owns household income, rent, utilities and arrears.
- `EnrollmentSystem` charges school costs to the government or a company owner.
- `HealthSystem` charges medical care to the sim/household.

## Design

### Guardian pays

- A dependent child's consumption and care are debited from the guardian household head, not from the child's empty wallet.
- Implement a `ChildCostService` used by `ConsumerSpendingSystem` and the health/care path:
  - resolve the child's guardian household;
  - debit the household head through `HouseholdFinanceService.TryDebit`;
  - record `ChildCostPaid` and the ledger entry;
  - if the household cannot pay, record unmet child demand/arrears and let needs/health decline.
- Do not credit the child; the money goes to the same company/government destinations as adult spending.

### Cost components

| Cost | Scaling | Destination |
|---|---|---|
| Food/household consumption | per child, existing category shares | companies |
| School | existing `School.MonthlyCost` | government or school owner |
| Health care | existing medical cost | government/provider |
| Housing space | unit capacity/overcrowding, not per child | landlord |

### Child benefit (optional)

If enabled:

- government pays a fixed monthly amount per dependent child to the guardian household;
- the payment is ledgered as `ChildBenefit`;
- the government budget and shortfall are visible;
- the benefit is not a mint and not a substitute for the existing safety net.

Default in V4a: **off** until the balance scenario shows a need.

### Conservation

- Every child cost is a transfer from the guardian household to an existing destination.
- Unpaid costs are arrears or unmet demand, never negative money.
- The 10-year balance scenario and the conservation test must remain green.

### Reporting

- `HouseholdFinanceService.MonthlyIncome` includes child benefit only when enabled.
- Household savings/rent burden metrics include child costs separately.
- The admin family page shows child costs by household and arrears.

## Tasks

- [ ] Add `ChildCostService`.
- [ ] Route dependent-child consumption through the guardian.
- [ ] Route child health/school costs through the existing paths.
- [ ] Add optional child benefit and ledger entry.
- [ ] Add unmet-demand and arrears handling.
- [ ] Add conservation and household-budget tests.
- [ ] Add child-cost fields to the read model.

## Files

- new `Genelife/Family/ChildCostService.cs`
- `Genelife/Systems/ConsumerSpendingSystem.cs`
- `Genelife/Housing/HouseholdFinanceService.cs`
- `Genelife/Systems/EnrollmentSystem.cs`
- `Genelife/Systems/HealthSystem.cs`
- `Genelife.UnitTests/Family/ChildCostTests.cs`

## Acceptance

- A child's consumption is paid by the guardian, not by the child's empty wallet.
- A household with insufficient funds accrues visible unmet demand/arrears.
- School and health costs continue to use their existing destinations.
- Child benefit, if enabled, is a ledgered government transfer.
- No child-cost path changes total money supply except the explicit benefit source.
- The balance scenario remains green.

## Testing

- Guardian pays for one and three children; compare budgets.
- Guardian with no funds and unmet demand.
- Enable child benefit and verify the ledger and government balance.
- Assert total money is unchanged by costs and changes only by the configured benefit source.
- Run the 10-year balance scenario with births enabled.
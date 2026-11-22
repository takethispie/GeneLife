# H03 — Households

**Phase:** 2 — Households
**Depends:** H01, H02
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Make the household the unit that shares housing costs, pools money and survives the death or departure of an individual.

## Context

`Wallet` is per sim. Payroll and pensions pay individuals. Rent, food and utilities are naturally shared. Without a household, a family would pay multiple rents and children could not be housed.

## Design

Household lifecycle:

- **Create** — a head plus optional partner, children and relatives.
- **Join** — a new member links to an existing household.
- **Leave** — a member moves out, marries in (future) or dies.
- **Dissolve** — last member leaves or dies; unit is released.
- **Merge** — V1 only during initial population generation, not at runtime.

Head selection:

1. explicit head if set;
2. otherwise the highest-earning adult member;
3. ties broken by lowest `NetworkId` for determinism.

Pooled finances:

```csharp
public sealed class HouseholdFinanceService
{
    public Money Cash(Entity household);
    public Money Savings(Entity household);
    public Money IncomePerCycle(Entity household);
    public int Earners(Entity household);
    public bool TryDebit(Entity household, Money amount, LedgerKind kind, out Money shortfall);
}
```

Debit order:

1. head wallet;
2. other adult wallets by descending balance;
3. household savings account;
4. record shortfall.

Dependents have wallets only if they receive money (student stipend is removed in V1; dependents are funded by the household). `TryDebit` never touches a child's wallet.

Household events:

`HouseholdFormed`, `HouseholdMemberJoined`, `HouseholdMemberLeft`, `HouseholdDissolved`, `HouseholdHeadChanged`.

## Tasks

- [ ] Add `HouseholdRepository` with create/join/leave/dissolve.
- [ ] Add `HouseholdFinanceService` with deterministic head selection.
- [ ] Enforce adult-head and dependent rules.
- [ ] Update `Residence` on every membership change.
- [ ] Emit household events and add projections for the admin.
- [ ] Add commands `POST /api/households`, `POST /api/households/{id}/members`, `DELETE .../members/{simId}`.
- [ ] Add a debug invariant: no sim in two households; no household without a living adult.
- [ ] Coordinate with estate settlement (life stages) so a death updates the household before inheritance.

## Files

- new `Genelife.API/Repositories/HouseholdRepository.cs`
- new `Genelife.API/Services/HouseholdFinanceService.cs`
- `Genelife/Components/Housing/Household.cs`, `HouseholdMember.cs`
- `Genelife.Messages/Events/Household*.cs`
- `Genelife.API/Endpoints/Wolverine/HouseholdEndpoints.cs`

## Acceptance

- A household always has exactly one living adult head unless it is dissolving.
- Adding a member updates `Residence` and the registry.
- Rent debit can draw from multiple adult wallets and savings in the documented order.
- A child cannot be a head and cannot be charged rent.
- Dissolving the last member releases the unit and clears occupancy.
- Head change is deterministic and evented.

## Testing

- Head selection matrix.
- Multi-wallet debit order.
- Child protection test.
- Dissolve/release test.
- Invariant checks.

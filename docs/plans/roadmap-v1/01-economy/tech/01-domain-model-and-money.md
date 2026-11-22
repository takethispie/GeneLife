# E01 — Money type and wallet migration

**Phase:** 1 — Foundation
**Depends:** none
**Estimate:** 1–2 sessions
**Status:** Planned

## Goal

Replace floating-point balances with an exact integer-cents `Money` value and migrate `Wallet` and every read model that exposes balances.

## Context

`Wallet` is `record struct { float Balance }` (`Genelife/Components/Gen/Wallet.cs`). `PayrollSystem` adds `employee.Salary` directly (`Genelife/Systems/PayrollSystem.cs:35`). Companies have no money at all. Float arithmetic over millions of wage and rent operations drifts, and a ledger cannot reconcile a drifting balance.

## Design

Create `Genelife/Values/Money.cs`:

```csharp
namespace Genelife.Values;

public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero = new(0);
    public decimal Units => Cents / 100m;
    public bool IsNegative => Cents < 0;

    public static Money FromUnits(decimal units) =>
        new((long)Math.Round(units * 100m, MidpointRounding.AwayFromZero));

    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));
    public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));
    public static Money operator *(Money a, int factor) => new(checked(a.Cents * factor));
    public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
    public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);
}
```

Migrate `Wallet`:

```csharp
public record struct Wallet
{
    public Money Balance;
    public Wallet(Money balance) => Balance = balance;
}
```

Keep `Employee.Salary` as `Money`; rename to `PayPerCycle` in a later step to reflect that payroll is quarterly. Existing float call sites convert explicitly with `Money.FromUnits(value)`.

## Tasks

- [ ] Add `Money` with operators, checked arithmetic and unit tests.
- [ ] Change `Wallet.Balance` to `Money`; update constructor and all call sites.
- [ ] Change `Employee.Salary` to `Money`; keep JSON DTOs in `decimal`.
- [ ] Update `PayrollSystem` to add `Money` instead of `float`.
- [ ] Update `SimRepository`, `SimulationEngine.CaptureState/Restore`, `PersistedSim.WalletBalance`, `WorldDataStatus`/`SimStatus` DTOs.
- [ ] Update `Genelife.Web/Models` to `decimal` and format with two decimals.
- [ ] Add a lint test that fails if `float` appears in money-named members (`Balance`, `Salary`, `Rent`, `Revenue`, `Wage`).

## Files

- new `Genelife/Values/Money.cs`
- `Genelife/Components/Gen/Wallet.cs`
- `Genelife/Components/Employment/Employee.cs`
- `Genelife/Systems/PayrollSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Persistence/PersistedWorldState.cs`
- `Genelife.API/DTOs/SimStatus.cs`, `EmployeeStatus.cs`
- `Genelife.Web/Models/WorldData.cs`

## Acceptance

- `dotnet build` succeeds with no implicit float-to-`Money` conversions.
- A wallet credited 0.10 three times holds exactly 0.30; no drift.
- Snapshot round-trip preserves cents exactly.
- Admin UI shows two-decimal money values.

## Testing

- `MoneyTests`: rounding midpoint, overflow, negative balances, equality.
- `PayrollSystemTests`: exact cents after 100 cycles.
- Persistence round-trip test with fractional-unit salaries.

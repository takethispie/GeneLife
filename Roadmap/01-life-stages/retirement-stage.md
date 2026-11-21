# Retirement Stage (Age 65-79)

Retired Genes have no employment. They live on pension income and accumulated savings, with reduced economic activity compared to Workers.

## Stage Entry

- Triggered by `StageProgressionSystem` when `Alive.Age >= 65`
- Components added: `Pensioner`, `RetirementSavings` (if not already present)
- Components removed: `Employee`, `JobSeeker`, `WorkSchedule` (handled by Work stage exit)

## Pension Calculation

Monthly pension is computed at retirement entry based on career summary:

```csharp
float ComputePension(Wallet wallet, SkillSet skills) {
    float savingsBonus = wallet.Balance * 0.01f;        // 1% of savings
    float skillBonus = skills.Skills.Values.Sum() * 50f; // 50f per skill point
    return 200f + savingsBonus + skillBonus;              // Base 200 + bonuses
}
```

Typical pension range: 300-600 per cycle.

## Pension System (`PensionSystem`)

- **Cadence:** Every 300 ticks (aligned with PayrollSystem)
- **Query:** `Pensioner` + `Wallet`
- **Behavior:** Add `pensioner.MonthlyPension` to `wallet.Balance`

## Retirement Behavior

Retired Genes behave like basic NPCs but with:
- No work obligation (no `WorkSchedule`, no `Working` action)
- Lower Energy consumption (0.7× normal decay rate — less activity)
- Same needs-driven decision making as base NPCs
- Still shop at stores via the consumer system

## Retirement Exit

At age 80, transitions to Elder stage. `Pensioner` component is removed; `Elderly` marker is added. Pension income stops — Elder Genes live entirely on remaining `Wallet.Balance`.

## Components

| Component | Fields | Notes |
|---|---|---|
| `Pensioner` | `float MonthlyPension` | Computed at retirement entry |
| `RetirementSavings` | `float TotalSaved` | Accumulated during Work stage (see Banking) |

## Acceptance Criteria

- At age 65, Worker transitions to Retired and `Pensioner` is attached
- `PensionSystem` adds pension to Wallet every 300 ticks
- Retired Genes have no Employment components
- Energy decays at 70% of normal rate for Retired Genes
- Pension amount reflects career earnings and skills
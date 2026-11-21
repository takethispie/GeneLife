# Economy Overview

The V1 economy is a closed-loop system: Genes earn wages, spend on consumption, companies earn revenue from spending, and revenue funds wages. Money circulates rather than appearing from nowhere.

## Economic Loop

```
                    ┌──────────────┐
                    │   Company    │
                    │   Wallet     │
                    └──┬──────┬────┘
            revenue    │      │    wages (PayrollSystem)
                    ┌──┘      └──┐
                    ▼            ▼
    ┌───────────────────┐   ┌──────────┐
    │  ConsumerSystem   │   │  Gene    │
    │  (spending at     │◄──│  Wallet  │
    │   stores/services)│   └──────────┘
    └────────┬──────────┘
             │ money
             ▼
    ┌───────────────────┐
    │  Company Wallet   │────► wages go back to Genes
    └───────────────────┘
```

## Existing Systems (Integrated)

| System | Status | Role |
|---|---|---|
| `PayrollSystem` | Exists (300t cadence) | Adds salary to Gene Wallet |
| `HiringSystem` | Exists (25t cadence) | Matches Workers to companies |
| `Wallet` (Gene) | Exists | Tracks individual Gene balance |

## New Systems

| System | Cadence | Role |
|---|---|---|
| `ConsumerSystem` | 100t | Genes spend money on goods/services |
| `RevenueSystem` | 300t | Companies earn from consumer spending |
| `BankingSystem` | 300t | Genes accumulate/deplete savings |
| `HousingSystem` | 600t | Genes pay housing costs based on wealth tier |

## Money Creation vs Circulation

V1 starts each Gene with a seed amount and companies with initial capital. Over time:

- **Money enters** the system via: initial seeds, student stipends (education), pension (retirement)
- **Money circulates** via: wages (company → Gene), spending (Gene → company)
- **Money leaves** via: death with no heirs (wallet destroyed), housing cost overhead

This is intentionally "leaky" — tweaking seeds and overhead rates balances the economy.

### Economic Controls

| Parameter | Default | Effect |
|---|---|---|
| `StudentStipendPerCycle` | 50f | Education income |
| `BasePensionPerCycle` | 200f | Minimum retirement income |
| `HousingBaseCost` | 100f | Per-cycle housing deduction |
| `SpendingRatePerCycle` | varies by stage | How much Genes spend |
| `RevenueMultiplier` | 1.2f | Company profit margin on spending |
| `InitialGeneBalance` | 500f | Starting money for new adult Genes |
| `InitialCompanyCapital` | 10000f | Starting money for new companies |

## Files

- [`consumer-spending.md`](consumer-spending.md) — How Genes spend money
- [`company-revenue.md`](company-revenue.md) — How companies earn and track revenue
- [`banking-and-housing.md`](banking-and-housing.md) — Savings, housing costs, pension deduction
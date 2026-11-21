# Company Revenue

Companies need a revenue stream to fund wages. In V1, revenue comes from consumer spending attributed to the company. This closes the economic loop: spending → revenue → wages → spending.

## Company Wallet

New component: `CompanyWallet` (record struct)

```csharp
struct CompanyWallet {
    float Balance;           // Current company funds
    float RevenueLastCycle;  // Revenue from last RevenueSystem pass
    float WagesLastCycle;    // Total wages paid last cycle
    float ProfitLastCycle;   // Revenue - Wages
}
```

Attached to each Company entity at creation.

## Initial Capital

When a company is created (via `CompanyRepository.Add()`), seed it with initial capital:

```csharp
float InitialCompanyCapital = 10000f;
world.Add(companyEntity, new CompanyWallet { Balance = InitialCompanyCapital });
```

## Revenue System (`RevenueSystem`)

- **Cadence:** Every 300 ticks (aligned with PayrollSystem — revenue and wages on same cycle)
- **Query:** `CompanyWallet` + `CompanyName`

### Revenue Collection

Each cycle, the system aggregates all `SpendingRecord.UnattributedAccum` from Genes attributed to this company and moves it to the company wallet:

```csharp
foreach (var (companyWallet, _, companyEntity) in companies) {
    var attributedGenes = genesAttributedToCompany[companyEntity];
    float totalRevenue = 0f;

    foreach (var gene in attributedGenes) {
        var spending = gene.SpendingRecord;
        totalRevenue += spending.UnattributedAccum;
        spending.UnattributedAccum = 0f;
        spending.TotalSpent += spending.UnattributedAccum;
    }

    companyWallet.RevenueLastCycle = totalRevenue;
    companyWallet.Balance += totalRevenue;
}
```

### Profit Tracking

After wages are paid (PayrollSystem runs), compute profit:

```csharp
// Called after PayrollSystem tick
companyWallet.ProfitLastCycle = companyWallet.RevenueLastCycle - companyWallet.WagesLastCycle;
```

### Wage Affordability

Before PayrollSystem pays wages, check if company can afford total wage bill:

```csharp
float totalWages = ComputeTotalWageBill(companyEntity);
if (companyWallet.Balance < totalWages) {
    // Only pay what's available, proportionally
    float ratio = companyWallet.Balance / totalWages;
    PayProportionalWages(companyEntity, ratio);
    companyWallet.Balance = 0f;
} else {
    PayFullWages(companyEntity);
    companyWallet.Balance -= totalWages;
}
```

If a company runs out of money:
- Wages are reduced proportionally
- `HiringSystem` sees a bankrupt company and stops posting new jobs
- Employees may quit (become `JobSeeker` again) if wages drop below 50% of expected

## Hiring Affordability

New job postings should only appear if the company can afford the salary:

```csharp
// In HiringSystem or a new CompanyJobSystem
bool CanAffordNewHire(CompanyWallet wallet, float proposedSalary) {
    float projectedAnnualWages = ComputeTotalWageBill(companyEntity) + proposedSalary;
    float projectedAnnualRevenue = wallet.RevenueLastCycle * cyclesPerYear;
    return wallet.Balance > proposedSalary * 3f; // 3-cycle buffer
}
```

## Bankruptcy

If a company has zero `CompanyWallet.Balance` for 5 consecutive `RevenueSystem` cycles:
- All `Employee` components on its Genes are removed
- All `OpenPosition` entities are destroyed
- Company entity is flagged as bankrupt (future: deletion, restructuring)
- V1: just flag it and stop all economic activity for that company

## Revenue Distribution

Consumer spending attribution to companies uses weighted random distribution based on company employee counts. This represents "market share" — bigger companies attract more consumer spending.

```csharp
Entity PickCompanyForAttribution(List<Entity> companies) {
    int totalEmployees = companies.Sum(c => employmentIndex.CountEmployees(c));
    int roll = Random.Shared.Next(totalEmployees);
    int cumulative = 0;
    foreach (var company in companies) {
        cumulative += employmentIndex.CountEmployees(company);
        if (roll < cumulative) return company;
    }
    return companies.Last();
}
```

### No-Company Fallback

If zero companies exist in the world, all consumer spending is destroyed (representing an informal/non-tracked economy). The RevenueSystem logs a warning but does not crash.

## Acceptance Criteria

- Companies have a `CompanyWallet` tracking balance, revenue, wages, profit
- `RevenueSystem` collects attributed consumer spending into company wallets each 300t cycle
- Wages are reduced if company cannot afford total wage bill
- Bankrupt companies stop hiring and paying full wages
- New companies receive 10,000f seed capital
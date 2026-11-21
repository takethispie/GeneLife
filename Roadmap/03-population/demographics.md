# Demographics Tracking

Track population statistics over time for debugging, balancing, and verifying simulation correctness. This is a passive observation system — it reads state, never mutates it.

## Demographics System (`DemographicsSystem`)

- **Cadence:** Every 3600 ticks (~2.4 minutes at 25 Hz)
- **Query:** `LifeStage` + `Alive`

### Tracked Metrics

```csharp
struct DemographicsSnapshot {
    long Tick;
    int TotalGenes;
    int Children;       // Age 0-5
    int Students;       // Age 6-17
    int Workers;        // Age 18-64
    int Retired;        // Age 65-79
    int Elders;         // Age 80+
    int EmployedWorkers; // Workers with Employee component
    int UnemployedWorkers; // Workers with JobSeeker, no Employee
    float UnemploymentRate; // UnemployedWorkers / Workers

    // Age stats
    float AverageAge;
    float MedianAge;
    int YoungestAge;
    int OldestAge;

    // Economic stats
    float TotalWealth;      // Sum of all Wallet.Balance + Savings
    float AverageWealth;
    float WealthGini;       // Gini coefficient (0-1)
    int HomelessCount;
    int BankruptCompanies;
}
```

### Logging

Each snapshot is logged to a simple in-memory ring buffer (last 100 snapshots). Accessible via an API endpoint for monitoring:

```
GET /api/demographics/current   → latest snapshot
GET /api/demographics/history   → last 100 snapshots
```

### Gini Coefficient Calculation

Approximate with sorted wallets:

```csharp
float ComputeGini(List<float> wealth) {
    wealth.Sort();
    float sumOfDifferences = 0f;
    float mean = wealth.Average();
    for (int i = 0; i < wealth.Count; i++)
        for (int j = 0; j < wealth.Count; j++)
            sumOfDifferences += Math.Abs(wealth[i] - wealth[j]);
    return sumOfDifferences / (2 * wealth.Count * wealth.Count * mean);
}
```

For V1 with thousands of Genes, this is fine. At scale (100k+), batch-sampling or incremental estimation can replace the O(n²) approach.

## Population Health Checks

`PopulationHealthSystem` runs every 7200 ticks and checks for simulation health:

| Check | Condition | Severity |
|---|---|---|
| Extinction | TotalGenes == 0 | Error |
| Collapse imminent | TotalGenes < 10 | Warning |
| No workers | Workers == 0 | Error |
| All unemployed | UnemploymentRate > 0.9 | Warning |
| Wealth drain | TotalWealth decreased 50%+ since last check | Warning |
| Ageing crisis | Ratio(Workers) / Ratio(Elders) < 2.0 | Warning |
| No companies | ActiveCompanies == 0 | Warning |

Health checks are logged but do not halt simulation in V1. Future versions can trigger world events or economy adjustments.

## Acceptance Criteria

- `DemographicsSystem` runs every 3600 ticks and captures a full population snapshot
- Snapshot includes age distribution, employment, wealth stats
- Gini coefficient is computed each cycle
- Snapshots are accessible via API endpoint
- Population health warnings fire for known degenerate states
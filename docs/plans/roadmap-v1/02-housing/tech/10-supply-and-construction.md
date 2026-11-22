# H10 — Supply and construction

**Phase:** 7 — Market
**Depends:** H05, H09, E14
**Estimate:** 2–3 sessions
**Status:** Planned

## Goal

Let housing supply respond to vacancy and demand over simulated years, so population growth and immigration do not end in permanent shortage.

## Context

A fixed stock means every new household competes for the same units, rents rise without limit and homelessness becomes structural. Construction is also a natural use for company capital and credit.

## Design

### Market index

`HousingMarketSystem` order 514, cadence 100:

```
targetVacancy = 0.05
index += clamp((targetVacancy − vacancyRate) × response, −maxStep, +maxStep)
index = clamp(index, 0.7, 1.6)
```

Rents for new tenancies and annual renewals use `index`. Existing tenancies adjust only at the year boundary, capped at ±10% per year.

### Construction

`HousingDevelopmentSystem` order 595, cadence 300:

Eligibility for a construction company:

- vacancy < 3%;
- expected rent at the target tier covers `constructionCost / paybackMonths` plus maintenance;
- company is `Healthy` or has approved credit;
- district capacity not exceeded.

Project:

```csharp
public record struct ConstructionProject
{
    public ulong ProjectId;
    public Entity Developer;
    public string District;
    public HousingTier Tier;
    public int Units;
    public Money Cost;
    public int MonthsRemaining;
    public long StartedAtTick;
}
```

- Cost is paid in instalments from the company account.
- On completion, create the property and units, add them to the registry, and emit `PropertyBuilt`.
- Cancellation refunds unspent capital and releases land capacity.
- Municipality can fund shelter construction from the government account when homelessness exceeds a threshold.

### Demolition and condemnation

Units below condition 0.2 are removed after relocation; the property may be demolished, releasing land capacity.

## Tasks

- [ ] Add `HousingMarketSystem` and market index to state.
- [ ] Add construction projects and the developer planner.
- [ ] Implement instalment payments and completion.
- [ ] Add district land capacity configuration.
- [ ] Add `PropertyBuilt`, `ConstructionStarted`, `ConstructionCompleted`, `ConstructionCancelled`.
- [ ] Add municipality shelter construction trigger.
- [ ] Add admin actions to start/cancel a project and adjust the index.
- [ ] Expose vacancy, index, pipeline and completions in the read model.

## Files

- new `Genelife/Systems/HousingMarketSystem.cs`, `HousingDevelopmentSystem.cs`
- new `Genelife/Components/Housing/ConstructionProject.cs`
- new `Genelife/HousingMarketOptions.cs`
- `Genelife.API/Services/HousingRegistry.cs`
- `Genelife.Messages/Events/PropertyBuilt.cs`, `Construction*.cs`
- `Genelife.API/Endpoints/Wolverine/HousingEndpoints.cs`

## Acceptance

- Low vacancy raises the index within bounds; high vacancy lowers it.
- Construction starts only when the economics and capacity allow.
- Completed projects create exactly the planned units and enter the registry.
- Cancellation refunds correctly and releases capacity.
- Shelter construction triggers when homelessness is high and government funds exist.
- Rents do not jump more than the annual cap for existing tenancies.

## Testing

- Index response test.
- Construction eligibility matrix.
- Instalment and completion accounting.
- Cancellation refund test.
- Municipality trigger test.
- Rent cap test.

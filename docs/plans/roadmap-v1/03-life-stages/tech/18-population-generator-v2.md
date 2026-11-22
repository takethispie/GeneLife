# L18 — Population generator v2

**Phase:** 7 — Continuity
**Depends:** L01–L17, H04, E03–E12
**Estimate:** 3 sessions
**Status:** Implemented (V1)

## Goal

Seed a realistic world in one deterministic pass: companies, offices, postings, households, housing, schools and an age pyramid with coherent roles and money.

## Context

Today the world starts empty and sims are created manually. The earlier Roadmap proposed a generator, but it assumed a different lifecycle and no housing/economy. V2 must produce a state that passes every invariant on tick zero.

## Design

`PopulationGenerator.Generate(World, GenerationConfig, seed)`:

1. Create government, pension fund and market singletons.
2. Create companies with capital and offices; open initial postings sized to expected worker demand.
3. Create schools with capacity matching the child population.
4. Create residential properties and units by tier, including shelter beds.
5. Generate sims by age band:
   - Education 18%;
   - Work 57%;
   - Retirement 19%;
   - Elderly 6%.
   Age within band is uniform, with a slight taper at the oldest ages.
6. Assign skills:
   - adults: distribution matching the job mix, with experience premium by age;
   - students: partial profiles;
   - retirees: frozen profiles and pension accounts consistent with age.
7. Form households and assign housing using `HouseholdAssigner`.
8. Attach employment for a share of workers; leave the rest as job seekers so hiring has work to do.
9. Attach pension accounts and `RetirementIncome` for retirees, with a funded share.
10. Run the invariant validator; fail generation if any check fails.

### Config

```csharp
public sealed class GenerationConfig
{
    public int Seed { get; set; } = 42;
    public int TotalSims { get; set; } = 1000;
    public int Companies { get; set; } = 10;
    public int Schools { get; set; } = 4;
    public decimal EducationShare { get; set; } = 0.18m;
    public decimal WorkShare { get; set; } = 0.57m;
    public decimal RetirementShare { get; set; } = 0.19m;
    public decimal ElderlyShare { get; set; } = 0.06m;
    public Money InitialCompanyCapital { get; set; }
    public Money InitialHouseholdCash { get; set; }
}
```

Shares must sum to 1.0 and be validated.

### Determinism

Every random choice uses `SimulationRandom` streams keyed by purpose. The same seed and config produce byte-identical component values across runs and machines.

## Tasks

- [ ] Implement the generator phases and configuration validation.
- [ ] Create fiscal, company, office, posting, school and property bootstrap.
- [ ] Generate the age pyramid and skill profiles.
- [ ] Form households and assign housing.
- [ ] Seed employment, unemployment and pensions consistently.
- [ ] Add the invariant validator and fail-fast report.
- [ ] Wire `POST /api/population/generate` and `POST /api/population/spawn`.
- [ ] Add a deterministic golden-file test for a small config.
- [ ] Document the generated starting state in the admin.

## Files

- new `Genelife.API/Services/PopulationGenerator.cs`
- new `Genelife/GenerationConfig.cs`
- `Genelife.API/Services/HouseholdAssigner.cs`
- `Genelife.API/Repositories/*`
- `Genelife.API/Endpoints/Wolverine/PopulationEndpoints.cs`
- `Genelife.UnitTests/Population/PopulationGeneratorTests.cs`

## Acceptance

- A generated world has the requested population and age shares within ±2%.
- Every sim has a stage, household and housing/shelter outcome.
- Every worker is employed or a job seeker; no student or retiree is employed.
- Company accounts and postings exist and are consistent with capacity.
- The invariant validator passes on tick zero.
- Same seed produces the same world; a different seed changes it.
- Generation completes in a few seconds for 1,000 sims.

## Testing

- Config validation tests.
- Age-share distribution test.
- Invariant validator test with deliberate corruption.
- Golden-file determinism test.
- 1,000-sim generation performance test.

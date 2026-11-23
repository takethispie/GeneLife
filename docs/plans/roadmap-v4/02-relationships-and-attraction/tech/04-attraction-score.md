# R04 — Attraction score

**Phase:** 4 — Scoring
**Depends:** R01–R03, T03 coefficients
**Estimate:** 2 sessions
**Status:** Done

## Goal

Return a deterministic, bounded, explainable compatibility score for two eligible sims.

## Context

The offline calibration tool (T03) produces integer coefficients. The runtime scorer consumes them and returns named components. It must not use `Microsoft.ML`, floats that differ across platforms, or random draws (acceptance is a separate step).

## Design

### Input

```csharp
public readonly record struct AttractionInput(
    ulong SimNetworkIdA,
    ulong SimNetworkIdB,
    Traits TraitsA,
    Traits TraitsB,
    int AgeA,
    int AgeB,
    bool SameHousehold,
    bool SameWorkplace,
    bool SameSchool,
    int TileDistance,
    decimal IncomeA,
    decimal IncomeB,
    bool EmployedA,
    bool EmployedB,
    bool HousedA,
    bool HousedB,
    int HealthA,
    int HealthB);
```

### Output

```csharp
public sealed record AttractionResult(
    int Score,                 // 0..10000
    IReadOnlyList<AttractionComponent> Components,
    bool Eligible,
    string? RejectionReason);
```

Each component is named and contributes an integer number of basis points. The displayed score is the sum; the admin sees the breakdown.

### Components

| Component | Max | Rule |
|---|---|---|
| Trait compatibility | 4,200 | Weighted inverse distance across the seven inheritable traits; age is separate |
| Age preference | 1,800 | Full score inside the preferred band and gap; linear falloff outside |
| Proximity | 1,200 | Same household > same workplace/school > nearby tiles > distant |
| Stability | 1,500 | Employment, income band and housing stability |
| Health | 800 | Current health band |
| Availability | 500 | Not partnered, not in cooldown, not recently rejected |

Trait distance for an enum with `n` values:

```text
distance = abs(indexA - indexB) / (n - 1)       // 0..1
compatibility = 1 - distance
```

Use integer arithmetic in basis points to avoid platform float drift:

```text
traitScore = sum(weight[trait] * (1000 - distanceBp[trait])) / totalWeight
```

### Eligibility

The scorer accepts an `EligibilityPolicy` result. If the pair is ineligible, it returns `Score=0`, `Eligible=false` and the named reason. It never scores an ineligible pair.

### Symmetry

The score is symmetric in V4a: both sims see the same compatibility value. Individual acceptance is a separate deterministic roll in the pairing system, so asymmetric preferences can be added later without changing the score contract.

### Why integer basis points

- Replays are identical across runs.
- The admin can display exact component contributions.
- Coefficient changes are testable.
- No hidden rounding from `float`/`double` formatting.

## Tasks

- [ ] Add `AttractionInput`, `AttractionResult` and `AttractionComponent`.
- [ ] Add `AttractionScorer.Score(input, coefficients, options)`.
- [ ] Add integer trait-distance tables.
- [ ] Add age, proximity, stability, health and availability components.
- [ ] Add a named rejection path for every eligibility failure.
- [ ] Add monotonicity and bounds tests.
- [ ] Add a fixture that pins a known pair's score.

## Files

- new `Genelife/Family/AttractionScorer.cs`
- new `Genelife/Family/AttractionInput.cs`
- new `Genelife/Family/AttractionResult.cs`
- `Genelife.UnitTests/Family/AttractionScoreTests.cs`

## Acceptance

- Scores are always in `[0, 10,000]`.
- Component contributions sum to the score.
- An ineligible pair returns a named reason and score 0.
- Improving a component never lowers the score (monotonicity).
- Identical inputs always produce the identical score.
- The score is symmetric for swapped inputs.
- The runtime scorer has no ML or file dependency.

## Testing

- Table-driven trait distance for every enum pair.
- Age band and gap boundaries.
- Proximity ordering: household, workplace, school, near, far.
- Stability and health boundary values.
- Swap A/B and compare.
- Pin a known pair's score and fail on accidental coefficient drift.
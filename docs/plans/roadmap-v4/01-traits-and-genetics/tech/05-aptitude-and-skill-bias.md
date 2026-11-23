# T05 — Aptitude and skill bias

**Phase:** 5 — Integration
**Depends:** T01, T02, T04
**Estimate:** 1–2 sessions
**Status:** Done

## Goal

Make `Aptitude` matter without duplicating `SkillProfile`. Aptitude biases starting proficiency and learning speed; skills remain the realised, trainable value.

## Context

`SkillProfile` holds six continuous families in `[0, 1]`: Programming, Art, Cooking, Medical, Engineering, Sales. It is created for seeded sims and trained by `EducationSystem` and work experience. There is no heritable aptitude today.

`AttractivenessService` calls its field “Intelligence”, but its sample values are domains (`Science`, `Art`). That is an aptitude, not a skill score.

## Design

### Aptitude policy

```csharp
public sealed class AptitudePolicy
{
    public decimal ScienceBonus { get; set; } = 0.10m;
    public decimal ArtBonus { get; set; } = 0.15m;
    public decimal CraftBonus { get; set; } = 0.10m;
    public decimal SocialBonus { get; set; } = 0.15m;
    public decimal LearningMultiplier { get; set; } = 1.15m;
}
```

Mapping:

| Aptitude | Starting bonus | Learning |
|---|---|---|
| `Science` | Medical +0.10, Engineering +0.10 | ×1.15 for Medical/Engineering |
| `Art` | Art +0.15 | ×1.15 for Art |
| `Craft` | Cooking +0.10, Engineering +0.10 | ×1.15 for Cooking/Engineering |
| `Social` | Sales +0.15 | ×1.15 for Sales |
| `Physical` | none in V4a | no skill multiplier |

`Physical` is reserved for future health/fitness use. V4a must not invent a false skill mapping for it.

### Application points

- `PopulationGenerator.AdultProfile` and `ChildProfile`: add the starting bonus after the base profile is generated.
- Newborn creation (feature 03): start from the aptitude bonus instead of a random profile.
- `EducationSystem`: multiply the gained amount by the aptitude learning multiplier for the matching skill family.
- Work experience: no change in V4a; performance and career systems remain the authority.

### Bounds

- Final proficiency is clamped to `[0, 1]` by `SkillProfile.Add`.
- The bonus is small enough that it shifts probabilities, not outcomes.
- Existing tests that assert exact seeded skill values may need fixture updates; tests that assert ranges must remain valid.

### Determinism

Aptitude comes from `Traits`, so skill bias is deterministic for a fixed seed. Do not add a separate random draw for the bonus.

## Tasks

- [ ] Add `AptitudePolicy` and validation.
- [ ] Add `AptitudeSkills.ApplyStartingBonus(ref SkillProfile, Aptitude)`.
- [ ] Add `AptitudeSkills.LearningMultiplier(Aptitude, Skill)`.
- [ ] Apply the starting bonus in the population generator and newborn factory.
- [ ] Apply the learning multiplier in `EducationSystem`.
- [ ] Add tests for each aptitude and for neutral `Physical`.
- [ ] Update affected lifecycle/education fixtures.

## Files

- new `Genelife/Family/AptitudePolicy.cs`
- new `Genelife/Family/AptitudeSkills.cs`
- `Genelife.API/Services/PopulationGenerator.cs`
- `Genelife/Systems/EducationSystem.cs`
- `Genelife.UnitTests/Family/AptitudeTests.cs`
- affected lifecycle tests and fixtures

## Acceptance

- Every seeded sim's starting profile includes the configured aptitude bonus.
- Education gain is measurably higher for the matching aptitude family.
- `Physical` does not change any skill.
- Proficiency never leaves `[0, 1]`.
- Same seed and traits produce the same starting skills.

## Testing

- Compare average Programming/Medical/Engineering proficiency for `Science` vs `Art` cohorts.
- Run education for 100 months and compare gained skill for matching vs non-matching aptitude.
- Assert no aptitude produces a value outside `[0, 1]`.
- Replay a fixed seed and compare profiles.
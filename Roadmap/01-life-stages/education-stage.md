# Education Stage (Age 6-17)

Genes in the Education stage attend school, gain skills, and have no economic output. They are consumers only (spending handled by guardian or basic allowance — V1 gives them a small stipend).

## Stage Entry

- Triggered by `StageProgressionSystem` when `Alive.Age >= 6`
- Components added: `LifeStage { Stage = Student }`, `SkillSet` (empty skills)
- Components removed: none (Child has no special components in V1)

## Education System (`EducationSystem`)

- **Cadence:** Every 50 ticks (~2 seconds at 25 Hz)
- **Query:** `LifeStage == Student`

### Behavior

Each tick, the Student:
1. Advances skill levels based on time spent in education
2. Consumes energy (Needs.Energy decay at 1.5× rate while studying)

### Skill Acquisition

Skills grow linearly over education years. The system uses a weighted random pick each cycle:

```csharp
// Skill pool with base weights — companies set RequiredSkills in JobPosting
var skillPool = new Dictionary<string, float> {
    { "General", 0.4f },
    { "Technical", 0.2f },
    { "Service", 0.2f },
    { "Management", 0.1f },
    { "Creative", 0.1f }
};
```

On each cadence pass, pick a random skill from the pool (weighted) and increment its level by a small amount (e.g., `0.05f` per 50t). Skills cap at `1.0f` during education.

At age 18 (stage exit), a typical Student will have a spread of skills totaling between 1.5 and 2.5 points across categories. This skill spread determines their competitiveness in the HiringSystem's `CalculateMatchScore`.

### Max Education Duration

A Student ages out at 18 regardless of skill level — there is no concept of "failing" or "dropping out" in V1.

## Components

| Component | Fields | Notes |
|---|---|---|
| `SkillSet` | `Dictionary<string, float> Skills` | Maps skill name → level (0.0-1.0) |
| `Institution` | `Entity SchoolEntity` | (V2) Links student to a school building |

## Integration

### HiringSystem Changes

`CalculateMatchScore()` currently uses a hardcoded `List<string> Skills` on `JobSeeker`. After this feature, it should use `SkillSet.Skills` instead:

```csharp
// Current (hardcoded):
float skillScore = ... // based on JobSeeker.Skills

// V1: compute intersection of SkillSet.Skills and JobPosting.Requirements
float skillScore = ComputeSkillMatch(applicantSkillSet.Skills, posting.Requirements);
```

Match score formula changes from 40% skills + 60% salary to weighted skill intersection + salary ratio. Exact weights remain at 40/60.

### Student Economic Activity

Students receive a small fixed stipend added directly to their Wallet each pay period (e.g., `50f` per 300t cycle). This funds basic consumption (food). The stipend comes from thin air in V1 — future versions will deduct from a parent/guardian Wallet.

## Acceptance Criteria

- A Gene created at age 6 gains skills automatically over time
- At age 18, `SkillSet.Skills` contains at least 3 skills with non-zero levels
- `HiringSystem.CalculateMatchScore()` uses `SkillSet` not `JobSeeker.Skills`
- Students consume Energy faster than Workers
- Students receive a stipend to their Wallet
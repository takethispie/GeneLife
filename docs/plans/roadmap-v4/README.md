# Roadmap V4 — Relationships, Attraction and Reproduction

**Status:** Planning (first page)
**Scope:** simulation, persistence and admin surfaces. No appearance rendering, no player pregnancy UI.
**Depends on:** V1 households/estates/population continuity, V2 spatial index, V3 read-model conventions.

---

## Why this is next

V1–V3 delivered a closed economy, housing, life stages, a 2D world and a browser client. The first item still missing from the README's planned features is **NPC relationships, attraction and reproduction**. Every prerequisite now exists:

| Prerequisite | Evidence |
|---|---|
| Households, rent and household finance | `Genelife/Components/Housing`, `HouseholdFinanceService` |
| Estates and inheritance on death | `EstateSettlementSystem` |
| Population continuity fallback | `PopulationInflowSystem`; births are explicitly excluded in `03-life-stages/business/06-population-continuity.md` |
| Neighbour/radius queries | `SpatialIndex`, V2 spatial grid |
| Trait feature schema | `AttractivenessService/` ML.NET prototype (currently orphaned, not in `GeneLife.sln`) |

Reproduction also gives the population a second continuity path: today the only long-run replacement is immigration, so the age pyramid and workforce depend entirely on inflow policy.

---

## Scope

**In scope**

- heritable trait model and trait generation for seeded sims and births;
- relationship components/index (parent, child, partner, household ties);
- deterministic attraction scoring and candidate selection;
- partnership formation, household merge/split and housing capacity;
- pregnancy, birth, childhood dependency and life-stage entry;
- trait inheritance and mutation;
- estate/housing changes when a partner or parent dies;
- admin read model, API, page, metrics and alerts;
- snapshot schema migration and deterministic restore.

**Out of scope (for V4a)**

- rendered appearance or portraits;
- player-controlled pregnancy/childcare UX;
- divorce/custody law, family-tree visualisation;
- running the ML.NET model inside the tick loop;
- multi-generation genealogy beyond what relationships and inheritance need.

---

## Shared design decisions

These are the decisions every V4 feature must follow. Change them here first.

### Trait model

The trait model is the schema shared by genetics, attraction and reproduction. It must mirror `AttractivenessService` so the existing model remains usable as an offline calibration oracle, while fitting the ECS.

Today the simulation has age (`Alive`, `Lifecycle`), a name and six continuous skills (`SkillProfile`). It has **no sex, physical traits or temperament**.

```csharp
namespace Genelife.Components.Gen;

public enum EyeColor : byte { Brown, Blue, Green, Grey, Hazel }
public enum HairColor : byte { Black, Brown, Blonde, Ginger, Grey }
public enum Handedness : byte { Right, Left, Ambidextrous }
public enum Morphotype : byte { Ectomorph, Mesomorph, Endomorph }
public enum Aptitude : byte { Science, Art, Craft, Social, Physical }
public enum HeightPotential : byte { Short, Average, Tall }
public enum Sex : byte { Female, Male }
public enum Temperament : byte { Peaceful, Neutral, Violent }

public record struct Traits
{
    public EyeColor EyeColor;
    public HairColor HairColor;
    public Handedness Handedness;
    public Morphotype Morphotype;
    public Aptitude Aptitude;
    public HeightPotential HeightPotential;
    public Sex Sex;
    public Temperament Temperament;
}
```

| Decision | Rule |
|---|---|
| Categorical storage | `byte`-backed enums in one `Traits` record struct. No strings: ECS storage, snapshot size and determinism. |
| Age | Never duplicated. Derived from `Alive.Age` / `Lifecycle.AgeTicks`. |
| Intelligence | `Aptitude` is a heritable aptitude that biases starting `SkillProfile`, not a second skill score. |
| Hair naming | The prototype's `HairType` values are colours; canonical name is `HairColor`, with a mapping adapter for the model. |
| Sex | V4 needs only pairing eligibility. Keep the enum minimal; do not infer identity or roles beyond that. |
| Inheritance (V4a) | Each trait takes one parent's value plus a small `SimulationRandom` mutation. Add a hidden two-allele `Genome` layer only if recessive traits become a requirement. |
| Genetic vs acquired | Genetics owns potential/aptitude (height potential, aptitude, temperament baseline). Runtime systems own realised values (skills, health, current height). |
| Attraction evaluator | Deterministic C# score in the tick loop. Coefficients are calibrated offline against `Attractiveness.mlnet`; no `Microsoft.ML` dependency at runtime. |
| Persistence | Traits are part of the snapshot; bump the schema version and backfill existing sims deterministically from world seed + `NetworkId`. |
| Performance | Precompute one attractiveness vector per sim per cadence; generate candidates from household/workplace/school/spatial indices. Never O(n²) over the population. |

`AttractivenessService` validates the field list but cannot ship as-is: it is not in the solution and loads a relative `.mlnet` file whose `.mbconfig` points at `D:\Dev\GeneLife\...`.

### Relationships

Relationships are directed facts with a type and optional household reference: `parent`, `child`, `partner`, `ex-partner`. Derived convenience links (siblings, grandparents) are computed, not stored. The relationship index is rebuilt on restore and updated only in apply phases, following `EmploymentIndex`.

### Determinism

All pairing, conception, trait inheritance and mutation draws use the economy `SimulationRandom` streams with a documented cadence. A 10-year run must replay identically from a seed.

---

## Delivery order

| Step | Deliverable | Depends on |
|---|---|---|
| 1 | Trait schema, generation for seeded sims, snapshot migration/backfill | — |
| 2 | Relationship components and index | 1 |
| 3 | Deterministic attraction score + offline calibration harness | 1, 2 |
| 4 | Partnership formation, household merge/split, housing capacity | 2, 3 |
| 5 | Pregnancy, birth, childhood dependency and life-stage entry | 4 |
| 6 | Trait inheritance and mutation on birth | 1, 5 |
| 7 | Estate/housing changes on partner or parent death | 2, 4, 6 |
| 8 | Births as a population-continuity path; immigration interaction | 5, 6 |
| 9 | Admin read model, API, page, metrics and alerts | 2–8 |
| 10 | Persistence, restore, determinism and decade acceptance run | all |

---

## Open questions

1. **Pairing policy.** Age gap, consanguinity, exclusivity and same-sex pairing rules. The sim needs a clear eligibility function before attraction can be tested.
2. **Births vs immigration.** Should births reduce the immigration budget one-for-one, or supplement it until the target population is reached? This changes the age pyramid.
3. **Mutation and alleles.** Is single-parent-per-trait enough for V4a, or is a hidden `Genome` with dominance required?
4. **Household capacity.** Units currently model occupancy implicitly. Can a couple and children share a unit without a new capacity field, or does `HousingUnit` need an explicit `Capacity`?
5. **Child costs.** Which existing costs (food, school, healthcare) scale with children, and who pays when the household cannot?
6. **Sex representation.** The simulation needs `Sex` only for reproduction eligibility. Decide whether any other system may read it, so the field does not quietly become a social-role switch.
7. **Calibration.** How will the deterministic C# score be validated against the ML model's outputs without shipping the model?

---

## Definition of done

- Seeded sims and births carry a deterministic `Traits` component; old snapshots migrate with a stable backfill.
- A 10-year run produces births, and population continuity works without relying solely on immigration.
- Partners form and households merge/split without breaking rent, housing capacity or estates.
- Children inherit traits with mutation; the same seed replays identically.
- Relationships and attraction outcomes are visible in the admin read model and page.
- The money conservation test and the balance scenario remain green.
- New unit tests cover trait generation, inheritance, eligibility, relationship indexing, birth, restore and the decade acceptance run.
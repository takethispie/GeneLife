# Traits and Genetics — Summary

**Folder:** `01-traits-and-genetics/`
**Status:** Implemented (V4 feature 01; calibration CLI remains offline tooling)

**Depends on:** V1 life stages and skills, V2 spatial indices, `PersistedWorldState` schema 5, `SimulationRandom`
**Feeds:** relationships and attraction, reproduction, continuity and inheritance, admin family page

## Objective

Give every sim a deterministic, heritable, observable trait set that the rest of V4 can read. Attraction, reproduction, family resemblance and eventual continuity all depend on one shared schema: which traits exist, how they are generated, how they are inherited, and how they survive save/restore.

This is a foundation feature. It does not add romance, pregnancy or a family page; it makes those features possible without inventing ad-hoc trait fields in each system.

## Baseline before V4

- A sim has `Alive.Age`, `Lifecycle`, `GenName`, `SkillProfile`, `Health`, needs, wallet and optional household/employment/enrollment state.
- There is no sex, physical trait, aptitude or temperament field.
- `AttractivenessService` defines the target feature schema for two partners but is not in `GeneLife.sln` and is not safe to run in the tick loop.
- `SimulationRandom` exists; `Random.Shared` is banned in simulation code.
- `PersistedWorldState` is schema 5 and already persists economy/housing options.
- New sims are created only by `PopulationGenerator` and `PopulationInflowSystem`.

## Deliverables

1. Canonical `Traits` component with byte-backed enums and validation.
2. Deterministic trait generation for seeded sims and immigrants.
3. A documented mapping from the `AttractivenessService` schema to `Traits`.
4. Offline calibration harness and coefficient fixture; no runtime ML dependency.
5. Parent-pair inheritance with bounded mutation and a replayable RNG stream.
6. Aptitude effects on starting and learned `SkillProfile`.
7. Snapshot schema 5 → 6 migration and deterministic trait backfill.
8. Trait metrics, invariants, admin read fields and acceptance tests.

## Architecture

```
world seed ──► traits.seed stream ──► Traits (seed, immigrant, newborn)
                                          │
                    ┌─────────────────────┼─────────────────────┐
                    ▼                     ▼                     ▼
              Attraction score      Skill/learning bias     Family resemblance
                    │                     │                     │
                    └─────────────── persisted in schema 6 ────┘
                                          │
                                Trait metrics and admin read model
```

Traits are immutable for a sim's lifetime except through birth generation. Realised values such as current skills, health, income and housing are owned by their existing systems; genetics owns potential and baseline tendencies only.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Trait domain model](tech/01-trait-domain-model.md) | Foundation |
| 02 | [Trait generation](tech/02-trait-generation.md) | Seeding |
| 03 | [Attractiveness model mapping](tech/03-attractiveness-model-mapping.md) | Calibration |
| 04 | [Inheritance and mutation](tech/04-inheritance-and-mutation.md) | Genetics |
| 05 | [Aptitude and skill bias](tech/05-aptitude-and-skill-bias.md) | Integration |
| 06 | [Trait persistence and migration](tech/06-trait-persistence-and-migration.md) | Durability |
| 07 | [Trait metrics and invariants](tech/07-trait-metrics-and-invariants.md) | Observability |
| 08 | [Traits acceptance](tech/08-traits-acceptance.md) | Verification |

## Business reading

- [Why inherited traits](business/01-why-inherited-traits.md)
- [Traits, fairness and privacy](business/02-traits-fairness-and-privacy.md)
- [Genetics and continuity](business/03-genetics-and-continuity.md)

## Definition of done

- Every living sim has exactly one valid `Traits` component.
- Seeded sims, immigrants and newborns all receive traits from the same deterministic generator.
- Two parents' traits produce a child with bounded mutation and a replayable result.
- `AttractivenessService` can be mapped to the canonical traits for offline calibration, but no ML code runs in the tick loop.
- Aptitude measurably biases starting skills without replacing `SkillProfile`.
- Schema 5 snapshots migrate to 6 with the same traits on every restore, and a second restore does not change them.
- Trait metrics and invariants are visible in the admin read model.
- Unit, persistence and replay tests pass.
# Roadmap V4 — Status

**Status:** In progress — feature 01 (traits and genetics) implemented; feature 02 next
**Builds on:** V1 economy/housing/life stages, V2 spatial world, V3 read-model conventions
**Scope:** traits and genetics, relationships and attraction, reproduction and family, continuity and inheritance, admin/hardening
**Primary risk:** family state touches households, housing capacity, estates, population continuity and the money ledger at the same time

## Delivery checklist

| Area | Status | Planned evidence |
|---|---|---|
| Trait schema and generation | Done (feature 01) | `Traits` component, seeded traits, unit tests |
| Inheritance and mutation | Done (feature 01) | Parent-pair inheritance, mutation bounds, replay test |
| Offline attraction calibration | Partial (feature 01) | Prototype mapping, runtime coefficients and scorer skeleton; calibration CLI still offline |
| Relationship components and index | Planned | Edge entities, mirror pairs, rebuild test |
| Eligibility and attraction score | Planned | Policy tests, score explainability, determinism test |
| Pairing and household merge | Planned (opposite-sex only) | Partner edges, move-in/out, capacity and rent tests |
| Pregnancy and birth | Planned | Pregnancy lifecycle, newborn creation, parent edges |
| Childhood and school entry | Planned | Dependent marker, age-gated enrollment, aging test |
| Child costs and household finance | Planned | Ledgered costs, arrears, no minting |
| Births vs immigration | Planned | Target-floor policy, population metrics |
| Widowhood and guardianship | Planned | Head reassignment, guardian assignment, minor share |
| Inheritance rules | Planned | Partner/child/relative/escheat split, conservation test |
| Family read model and admin page | Planned | `/api/family`, `Family.razor`, health checks |
| Events and Marten projections | Planned | Family projection documents and replay |
| Persistence and schema 5 → 6 | Done (feature 01) | Trait fields, deterministic backfill, double-restore test |
| Determinism and scale | Partial (feature 01) | Trait generation replay and balance scenario run; 2,000-sim family budget pending |
| Release acceptance | Planned | Full suite, balance scenario, admin smoke test |

## Verification plan

| Check | When |
|---|---|
| `dotnet build GeneLife.sln` | every tech step |
| `dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-launch-profile` | every feature completion |
| `BalanceScenarioTests` 10-year report | before and after V4 continuity changes |
| Family acceptance suite | after feature 03 |
| Schema 5 → 6 migration and restore tests | after feature 05 persistence step |
| 2,000-sim family performance scenario | before release |
| Admin family page smoke test | before release |
| Full integration suite with PostgreSQL (`docker compose up -d postgres`) | before release |

Feature 01 verification (2026-11-23): `dotnet build GeneLife.sln` succeeds; `Genelife.UnitTests.Family`, `Persistence`, and the core simulation namespaces pass. The Marten/integration/performance namespaces still require PostgreSQL and were not part of this pass.

## Current baseline

- `PersistedWorldState.CurrentSchemaVersion` is 6, with persisted economy/housing/genetics options, the `BalanceBackfilled` migration and deterministic trait backfill from schema 5.
- Every production creation path (seed, immigrant, player, admin) writes a `Traits` component; inheritance and mutation helpers exist for feature 03 births.
- `HouseholdRole.Child` and `Tenure.Dependent` already exist but are not tied to parent edges.
- `HousingUnit.Capacity` and `OccupantCount` already exist; the generator still creates one household per adult.
- `DeathProcessingSystem` inherits through current household membership, not relationships.
- `PopulationInflowSystem` is the only population-continuity path.
- `AttractivenessService` is an orphaned ML.NET prototype outside `GeneLife.sln`; runtime attraction uses integer coefficients and never loads the model.
- The 2026-11 balance scenario passes conservation and money growth after the trait integration; wealth Gini, vacancy and company failures remain open but are not V4 blockers.

## Decisions to confirm before implementation

1. Same-sex pairing is not allowed in V4a. Only opposite-sex pairs are eligible for partnership and conception; adoption/surrogacy are deferred. Revisit this policy before enabling same-sex pairing.
2. Divorce is limited to admin-driven or death-driven `ExPartner` transitions in V4a.
3. Orphans go to a guardian, then relatives, then the existing shelter system; no new orphanage entity by default.
4. Child benefits are optional; school and health costs scale first.
5. Inheritance default is partner-first with children sharing the residual; the exact split is set in feature 04.
6. Mutation rate is calibrated so family resemblance remains visible over three generations.

## Known risks

- **Household capacity.** A birth can overcrowd a unit. The plan allows temporary overcrowding and surfaces it as housing demand rather than blocking birth.
- **Legacy saves.** Existing households have no parent edges. The migration can mark family data as restored but cannot invent parents; legacy children remain household-linked only.
- **Performance.** Attraction must not become an all-pairs scan. Candidate caps and quarterly vectors are mandatory.
- **Money loop.** Child costs and inheritance are easy places to mint or destroy money accidentally. Every transfer must be ledgered and the conservation test must stay green.
- **Fairness.** Trait visibility and pairing policy need explicit, non-judgmental boundaries; no public trait ranking.

## Next

1. Implement feature 02, steps R01–R03 (relationship domain model, index, eligibility with opposite-sex pairing).
2. Add relationship persistence and the eligibility policy tests before pairing and household merge.
3. Complete the offline calibration CLI when the relationship fixture lands; runtime scoring already uses pinned integer coefficients.
4. Start feature 03 conception/birth only after partner edges and parent/child edges replay cleanly.
# Roadmap V4 — Status

**Status:** V4 release-ready — features 01–05 implemented and hardened; `CohabitOnPairing` remains an optional follow-up
**Builds on:** V1 economy/housing/life stages, V2 spatial world, V3 read-model conventions
**Scope:** traits and genetics, relationships and attraction, reproduction and family, continuity and inheritance, admin/hardening
**Primary risk:** family state touches households, housing capacity, estates, population continuity and the money ledger at the same time

## Delivery checklist

| Area | Status | Planned evidence |
|---|---|---|
| Trait schema and generation | Done (feature 01) | `Traits` component, seeded traits, unit tests |
| Inheritance and mutation | Done (feature 01) | Parent-pair inheritance, mutation bounds, replay test |
| Offline attraction calibration | Partial (feature 01) | Prototype mapping, runtime coefficients and scorer skeleton; calibration CLI still offline |
| Relationship components and index | Done (feature 02) | Edge entities, mirror pairs, rebuild test |
| Eligibility and attraction score | Done (feature 02) | Policy tests, scorer components, admin explanation |
| Pairing and household merge | Done (feature 02; opposite-sex only) | Partner edges, capacity-aware merge, cohabitation opt-in |
| Pregnancy and birth | Done (feature 03) | Conception, pregnancy care/risk, newborn factory, parent edges |
| Childhood and school entry | Done (feature 03) | Dependent marker, age gates, adulthood transition |
| Child costs and household finance | Partial (feature 03) | Guardian-paid dependent consumption and conservation tests; child benefit/staged finance pending |
| Births vs immigration | Partial (feature 03) | Birth path live; yearly birth-rate metric and target tuning pending |
| Widowhood and guardianship | Done (feature 04) | Partner end, head/guardian reassignment, state care |
| Inheritance rules | Done (feature 04) | Relationship heir order, trusts, housing transfer, conservation |
| Family read model and admin page | Done (feature 05) | Family/pregnancy/child/guardian/trait/household/attraction reads, paging and filters, `Family.razor` |
| Events and Marten projections | Done (feature 05) | Family/relationship/pregnancy/child/household/estate events and idempotent projections |
| Persistence and schema 5 → 11 | Done (features 01–05) | Traits (v6), relationships (v7), pregnancies/births (v8), trusts/inheritance (v9), state care and ended edges (v10), `HasWallet` (v11), restore tests |
| Determinism and scale | Done (features 01–05) | Decade replay, seed sweep, legacy migration replay, 200-sim decade, 2,000-sim 1-year budget, 5,000-sim smoke, 2,000-sim ten-year run (p95 ~3.1 ms) and 30-year continuity |
| Release acceptance | Done (feature 05) | Full PostgreSQL suite 531/0/2, both release scenarios and admin HTTP smoke recorded in [release evidence](05-admin-observability-and-hardening/release-evidence.md); browser E2E not automated |

## Verification plan

| Check | When |
|---|---|
| `dotnet build GeneLife.sln` | every tech step |
| `dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-launch-profile` | every feature completion |
| `BalanceScenarioTests` 10-year report | before and after V4 continuity changes |
| Family acceptance suite | after feature 03 |
| Schema 5 → 11 migration and restore tests | after feature 05 persistence step |
| 2,000-sim family performance scenario | done (release evidence) |
| Admin family page smoke test | done at HTTP level (no browser automation) |
| Full integration suite with PostgreSQL (`docker compose up -d postgres`) | done (531 total, 0 failed, 2 skipped) |

Feature 01 verification (2026-11-23): `dotnet build GeneLife.sln` succeeds; `Genelife.UnitTests.Family`, `Persistence`, and the core simulation namespaces pass.

Feature 02 verification (2026-11-23): relationship domain/index/eligibility/scorer/candidate/merge/persistence/admin tests pass; the 60- and 200-sim balance scenarios pass with `CohabitOnPairing = false`.

Feature 03 verification (2026-11-23): pregnancy domain, conception rate/replay, prenatal care/risk/loss, birth/newborn, childhood gates, guardian-paid child consumption, pregnancy restore and reproduction invariants pass. The 200-sim decade remains green with births enabled.

Feature 04 verification (2026-11-23): heir order/shares, minor trusts/release/escheat, widowhood head/guardian transition, relative/state care, trust restore and continuity invariants pass. Births are counted in the population snapshot, immigration pauses with `TargetReached`, and the 200-sim decade remains green. Marten/integration namespaces still require PostgreSQL.

Feature 05 verification (2026-11-23): family read model, paged/filtered admin routes, typed audited operations, `Family.razor`, Marten family projections, schema 11 persistence (state care, ended edges, `HasWallet`), decade replay, seed sweep, legacy migration replay, dangling-edge detection and projection coverage pass. Full PostgreSQL suite is 531 total / 0 failed / 2 skipped (~73 s); the 2,000-sim ten-year run holds p95 ~3.1 ms with a clean restore and no health `Fail`, and the 30-year continuity run records zero continuity/reproduction failures. Admin family reads and mutations pass at HTTP level (unauthorized read 401, web `/family` 200); no browser automation was available. See [release evidence](05-admin-observability-and-hardening/release-evidence.md) and [release checklist](05-admin-observability-and-hardening/release-checklist.md).

## Current baseline

- `PersistedWorldState.CurrentSchemaVersion` is 11, with persisted family/inheritance/guardianship policies, relationship edges (including ended tick), pregnancy/birth history, minor trusts, state care, a `PersistedSim.HasWallet` flag, yearly birth/continuity metrics, the `BalanceBackfilled` migration, deterministic trait backfill from schema 5, `HasWallet` backfill from schema ≤ 10 and relationship/pregnancy/trust/state-care restore from schemas 6–9.
- Family read model is live-state authoritative; Marten `FamilyDocument`, `RelationshipDocument`, `PregnancyDocument`, `ChildDocument`, `HouseholdCompositionDocument` and `EstateDocument` are rebuildable history and are registered inline.
- The admin family page is admin-only and exposes traits, relationships, attraction, pregnancies, children, guardianship, household composition, health, projection staleness and tick/candidate budgets. Auto-refresh reloads only the active tab every ten seconds.
- Every production creation path (seed, immigrant, player, admin) writes a `Traits` component; inheritance and mutation helpers exist for feature 03 births.
- Seeded worlds create opposite-sex partner edges and parent edges. Partnered adults can conceive, carry pregnancies through prenatal care and give birth to complete newborns with inherited traits and parent edges.
- `CohabitOnPairing` still defaults to false: enabling it breaks the strict balance bands (wage share 0.9078 → 1.0527, vacancy 0.0854 → 0.2152) and worsens the 30-year run (overcrowded 1 → 12, vacancy 20.12% → 31.65%, health warnings 4 → 27). It stays opt-in until economy/consumption is recalibrated and seeded-pair cohabitation is added; this needs more than a housing tweak.
- `HouseholdRole.Child` is tied to parent edges for seeded and born children; a `DependentChild` marker identifies dependants. `Tenure.Dependent` remains unused for adult-headed households.
- `HousingUnit.Capacity` and `OccupantCount` already exist; the generator still creates one household per adult and releases adult children monthly under policy.
- `DeathProcessingSystem` inherits through current household membership, not relationships.
- Births and `PopulationInflowSystem` both add population; the target is a floor and immigration pauses with `TargetReached` when the gap closes.
- Death ends active partnerships, selects a guardian, transfers owned homes and settles relationship-based heirs; minor shares enter trusts released at adulthood.
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

1. Recalibrate economy/consumption and add seeded-pair cohabitation so `CohabitOnPairing` can default to true without breaking the strict balance bands; re-run the 30-year continuity scenario before flipping the default.
2. Add browser-level automated coverage for the admin family page; current smoke is HTTP-only.
3. Carried-forward Warnings (non-blocking): 61 health warnings in the 2,000-sim ten-year run and four reproduction `Warn` levels in the 30-year run are documented signals, not Failures.

V4 is otherwise release-ready. See [release evidence](05-admin-observability-and-hardening/release-evidence.md).

# R03 — Eligibility and seeded family policy

**Phase:** 3 — Policy
**Depends:** R01, R02, feature 01
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Define who may pair, who may not, and how the initial population gets a family structure instead of random child assignment.

## Context

`PopulationGenerator.BuildHouseholds` creates one household per adult and assigns children to random adult households. There are no parent edges, so a child can end up with an unrelated adult and two children in the same household need not be siblings.

Eligibility must also prevent pairing minors with adults, close relatives, and sims who already have a partner.

## Design

### Eligibility policy

```csharp
public sealed class RelationshipOptions
{
    public int MinimumPairingAge { get; set; } = 18;
    public int MaximumPairingAge { get; set; } = 70;
    public int MaxAgeGap { get; set; } = 15;
    public bool AllowSameSexPairing { get; set; } = true;
    public decimal SeedPartnershipShare { get; set; } = 0.60m;
    public int PairingThreshold { get; set; } = 5_500;
    public int MaxCandidatesPerSim { get; set; } = 24;
    public int RejectionCooldownMonths { get; set; } = 6;
}
```

Eligibility checks, in order:

1. Both sims alive, not dead, not `Tenure.Dependent`.
2. Both at least `MinimumPairingAge` and at most `MaximumPairingAge`.
3. Age gap within `MaxAgeGap`.
4. No current `Partner`.
5. Not parent/child, sibling, grandparent, aunt/uncle or first cousin.
6. Same-sex allowed only if `AllowSameSexPairing`.
7. Neither sim is in a pairing cooldown with the other.

Eligibility returns a named reason for rejection so the admin can explain it.

### Consanguinity

Use `RelationshipIndex`:

- direct parent/child and grandparent/grandchild: reject;
- siblings (share a parent): reject;
- aunt/uncle/niece/nephew: reject;
- first cousins (share a grandparent): reject by default;
- no common ancestor within the configured depth: allow.

The policy is configured, not hard-coded. Same-sex pairing does not change consanguinity.

### Seeded family structure

Replace random child assignment with a deterministic seed family:

1. Split seeded adults into an eligible pool by age and sex policy.
2. Sort by `NetworkId`.
3. Pair a deterministic share (`SeedPartnershipShare`) of the pool, preferring similar ages and compatible traits, using the same eligibility rules.
4. Create partner edges with `RelationshipSource.Seed`.
5. Attempt a household merge (R07).
6. Assign each seeded child to a paired household when possible; create parent edges to both partners.
7. If no pair is available, assign the child to a single adult and create one parent edge with `RelationshipSource.Seed`.
8. Preserve enough single adults for later pairing and for the labour market.

Seeded family structure uses `SimulationRandom.For("relationships.seed")` keyed by the sorted network ids; it never depends on creation order.

### Legacy saves

- Do not invent parent edges for old saves.
- Mark restored household roles as `RelationshipSource.Migration`.
- Treat a legacy child's household head as a guardian for inheritance and school decisions until a relationship is explicitly added.
- Surface the gap in the family health check.

## Tasks

- [ ] Add `RelationshipOptions` and validation.
- [ ] Add `EligibilityPolicy.Evaluate(a, b)` with named reasons.
- [ ] Add consanguinity queries using the relationship index.
- [ ] Replace random child assignment in `PopulationGenerator.BuildHouseholds`.
- [ ] Add seeded partnership and parent edges.
- [ ] Add a deterministic seed-family test fixture.
- [ ] Add legacy migration handling and health warning.

## Files

- new `Genelife/Family/RelationshipOptions.cs`
- new `Genelife/Family/EligibilityPolicy.cs`
- `Genelife.API/Services/PopulationGenerator.cs`
- `Genelife.UnitTests/Family/EligibilityTests.cs`
- `Genelife.UnitTests/Family/SeedFamilyTests.cs`

## Acceptance

- Minors, dependants, existing partners and close relatives are rejected with named reasons.
- Seeded children have at least one parent edge; paired children have two.
- Seeded partnerships are deterministic for a fixed seed.
- The seeded family does not consume all eligible adults.
- Legacy saves remain loadable and are flagged in the family health check.
- Same-sex policy is applied consistently to eligibility and seeding.

## Testing

- Table-driven eligibility tests for every rejection reason.
- Build a three-generation family and test cousin/sibling/ancestor rejection.
- Generate the same seed twice and compare partner and parent edges.
- Generate with `SeedPartnershipShare` 0 and 1.
- Load a legacy household fixture and assert the migration warning.
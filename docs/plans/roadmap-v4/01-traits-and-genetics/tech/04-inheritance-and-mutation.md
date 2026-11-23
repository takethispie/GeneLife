# T04 — Inheritance and mutation

**Phase:** 4 — Genetics
**Depends:** T01, T02
**Estimate:** 2 sessions
**Status:** Done

## Goal

Produce a child's traits from two parents deterministically, with bounded mutation and visible family resemblance.

## Context

V4a uses one value per trait and a single-parent-per-trait inheritance rule. Full alleles and dominance are deferred. The rule must be simple enough to explain to an operator and stable enough to replay.

## Design

### Algorithm

For each trait independently:

1. Choose the contributing parent with a deterministic coin.
2. Copy that parent's value.
3. With probability `MutationRate`, replace it with a different defined value for that trait.
4. If the trait has only one defined value, mutation is a no-op.

```csharp
public static Traits Create(
    Traits parentA,
    Traits parentB,
    int worldSeed,
    ulong childNetworkId,
    GeneticsOptions options);
```

### Deterministic seeding

- Use `SimulationRandom.For("traits.inheritance")` as the stream registry.
- Derive a per-child `Random` from `worldSeed`, `childNetworkId` and the trait name, so adding a trait does not shift the rolls of existing traits.
- Do not derive from query order, parent list order or creation order. If parent order is not stable, sort by `NetworkId` before calling.

### Mutation details

- `MutationRate` is `0..1`, default `0.04`.
- Mutation selects a different enum value by index:
  `next = (current + 1 + roll % (count - 1)) % count`.
- The roll uses the same per-child stream but a separate trait-keyed seed.
- A mutation never produces an undefined value or a value outside the enum.
- Mutation is recorded in a `TraitMutation` counter for metrics.

### Parent selection

- V4a inherits from exactly two parents.
- If one parent is unavailable (legacy save or death before birth), the child inherits from the available parent; if none, generate a seed trait set and mark `Source=Restored`.
- Parent order is canonicalized by `NetworkId` before generation.

### Optional genome layer

Do not add `Genome` in V4a. Leave a documented extension point:

```csharp
// Future: Traits could be expressed from a two-allele Genome.
```

The `Traits` component remains the read surface either way.

## Tasks

- [ ] Add `TraitsInheritance.Create` with the per-trait rule.
- [ ] Add `GeneticsOptions.MutationRate` validation.
- [ ] Add `TraitMutation` metrics counters.
- [ ] Add parent-order canonicalization.
- [ ] Add a fallback for a missing parent.
- [ ] Add distribution and reproducibility tests.
- [ ] Document that alleles/dominance are deferred.

## Files

- new `Genelife/Family/TraitsInheritance.cs`
- `Genelife/Family/GeneticsOptions.cs`
- `Genelife.UnitTests/Family/TraitInheritanceTests.cs`

## Acceptance

- A child has exactly one value per trait and every value is defined.
- With mutation rate 0, each child trait equals one parent's trait.
- With a configured mutation rate, the observed rate is within tolerance over a large sample.
- The same parents, seed and child id produce identical traits on every run.
- Changing trait order or adding a new trait does not change existing trait results for a fixed child id.

## Testing

- Inherit 10,000 children and assert the mutation rate and value bounds.
- Replay the same child id twice and compare.
- Test a single-value trait with a nonzero mutation rate.
- Test a missing-parent fallback.
- Test that parent order does not affect the result.
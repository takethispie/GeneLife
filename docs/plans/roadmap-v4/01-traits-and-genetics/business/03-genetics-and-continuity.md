# Genetics and continuity

A population that only replaces itself through immigration has no family continuity. Every new adult arrives from outside, and the age pyramid is a policy output rather than a consequence of births and deaths. Traits make the alternative possible: children inherit from parents, families persist across generations, and the population can grow from within.

## What inheritance buys

- **Continuity.** Parents pass traits to children, so a family line has a durable identity.
- **Diversity.** Mutation and partner choice keep the population from collapsing into a few repeated profiles.
- **A natural population path.** Births can share the continuity burden with immigration instead of leaving immigration as the only lever.
- **Long-run testability.** A 10- or 30-year run can assert birth rate, replacement rate, household size and trait diversity.

## V4a inheritance rule

For each trait, a child takes one parent's value, then applies a bounded mutation with a small probability. This is deliberately simpler than two-allele dominance:

- it is easy to explain;
- it is easy to test;
- it keeps family resemblance visible;
- it avoids a hidden state layer that the admin cannot inspect.

A future version can add a `Genome` layer with two alleles per trait and dominance without changing the `Traits` read surface.

## Mutation

Mutation exists to prevent a closed population from becoming static. It is not a gameplay slot machine.

- The rate is configured, not hard-coded.
- The mutation chooses a different value from the same trait's enum.
- The draw uses `SimulationRandom` with a documented stream.
- The rate is visible in trait metrics.
- A mutation never changes a trait outside its enum or produces an invalid value.

## Why not full genetics

Full genetics would add alleles, dominance, linkage, polygenic traits and genetic disease. Each increases the state surface, the migration burden and the explainability cost. V4a needs a family graph and a believable inheritance signal, not a research model. The trait schema is the extension point; the inheritance engine can be replaced later while the rest of V4 keeps working.

## Success signal

After a decade, the population should show:

- children who resemble parents more than unrelated sims;
- a stable trait distribution rather than one dominant value;
- a mutation rate high enough to avoid stagnation but low enough to keep families recognisable;
- a birth path that contributes to population continuity without breaking the money or housing loops.
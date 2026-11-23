# Why inherited traits

V1 and V2 make a sim a collection of state: age, money, skills, health, job, home and position. Those values are produced by systems and are mostly independent between sims. Nothing explains why one sim is more likely to become an engineer, why two sims are compatible, or why a child resembles a parent.

Traits give the population a second layer of identity: a small, stable set of inherited and expressed characteristics that other systems can read.

## What traits add

- **Family resemblance.** A child can inherit an aptitude or temperament, so a family has continuity beyond a shared surname.
- **Explainable attraction.** The attraction score can say which traits and circumstances contributed, rather than returning an opaque number.
- **Variation without noise.** A small enum set produces meaningful differences without turning every sim into a snowflake.
- **A stable genetic substrate.** Future features such as health predispositions, education aptitude or appearance can read the same fields instead of inventing new ones.
- **Testable inheritance.** A parent-pair generator and a mutation bound are easy to test and replay.

## What traits are not

- They are not a player-facing score or ranking.
- They are not a substitute for skills, health, income or housing.
- They are not a statement about a person's worth.
- They are not a full genetic simulation with alleles, dominance and polygenic traits in V4a.

## Design consequence

The smallest useful model is better than a realistic one that cannot be calibrated or tested. V4a chooses eight categorical traits, one value per sim, a single-parent-per-trait inheritance rule and a bounded mutation. The model can grow later without breaking the family graph.

## What the player or operator should notice

- Children resemble their parents in a few visible ways.
- Partners are not random; their traits and circumstances explain part of the match.
- Different families develop different aptitudes and temperaments over time.
- The admin can inspect why a pairing scored the way it did.
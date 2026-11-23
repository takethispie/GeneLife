# Attraction without a black box

Attraction is the part of V4 most likely to become either arbitrary or opaque. A random pair generator is arbitrary; an ML model in the tick loop is opaque and operationally heavy. V4 chooses a third path: a small, deterministic score with named components.

## What the score answers

“How compatible are these two sims, given their traits, ages, circumstances and proximity?”

It does not answer “who is the best sim in the world?” There is no global ranking. The score is local to a candidate pair and is used only when both sims are eligible.

## Components

| Component | What it captures |
|---|---|
| Trait compatibility | Distance between trait values, weighted by the calibrated model |
| Age preference | A configured acceptable age range and gap |
| Proximity | Same household, same workplace or school, nearby tiles |
| Stability | Employment, income and housing stability |
| Health | Current health band |
| Availability | Not already partnered, not in cooldown |

Each component returns a bounded contribution. The final score is an integer from 0 to 10,000.

## Why explainable matters

- An operator can see why a pair scored highly or poorly.
- A balance change is a coefficient change, not a retrain.
- A replay bug is traceable to a component, not hidden in a model.
- The score can be tested with monotonicity and bounds.
- The client never needs to ship a model artifact.

## What the score must not do

- It must not use sex, income or housing as a hidden proxy for a value judgement.
- It must not create a public attractiveness ranking.
- It must not use wall-clock time, query order or ambient randomness.
- It must not let one component dominate so completely that the others are decorative.

## Mutual choice

A high score is necessary but not sufficient. Both sims must accept the pairing through a deterministic roll. A rejection is remembered for a cooldown so the same pair does not retry every quarter.

## Calibration without runtime ML

The existing ML prototype is used offline to suggest weights. The resulting coefficients are checked into the repository with a version. The runtime scorer is plain C# and has no ML dependency.

## What good looks like

- Most eligible sims pair within a few years.
- Pairings are not uniformly random; trait and circumstance explain some of the variance.
- Close relatives and minors never pair.
- The same seed and options produce the same partnerships.
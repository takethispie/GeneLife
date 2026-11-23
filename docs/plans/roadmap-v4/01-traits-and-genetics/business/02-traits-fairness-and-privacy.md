# Traits, fairness and privacy

Traits are sensitive-sounding data even in a simulation. The project needs a clear boundary before any field is added: what the simulation may read, what the admin may see, and what the player-facing client must never imply.

## Principles

1. **Simulation input, not human judgement.** Traits exist to drive inheritance, attraction and aptitude. They do not rank people or justify unequal treatment outside the configured rules.
2. **Minimal representation.** `Sex` is needed for reproduction eligibility only. No other system may use it for pay, hiring, housing or social role.
3. **No public ranking.** The player client does not expose a trait score, a beauty score or a compatibility leaderboard. The admin may inspect traits for debugging and balance.
4. **Explainability over mystery.** An attraction decision should be explainable from a small set of named components. A black-box model is not acceptable in the tick loop.
5. **Deterministic fairness.** The same seed and options produce the same population. Pairing is not secretly weighted by query order or wall-clock timing.
6. **Sensitive combinations stay out of scope.** V4a does not model ethnicity, disability, religion or other protected characteristics as traits.

## Admin visibility

The family admin page may show:

- trait values for a selected sim;
- parent and partner edges;
- the attraction score breakdown for a candidate pair;
- aggregate distributions and mutation rates.

It must not show:

- a global “most attractive” ranking;
- trait-based sorting as a default;
- any suggestion that a trait is a defect or a score of human worth.

## Player visibility

A later player-facing slice, outside V4a, may show family relationships and life events, not trait numbers. If traits become visible later, they should be framed as family resemblance or preferences, not as a rating.

## Privacy and persistence

Traits are simulation state and are persisted like age or skills. They are not secrets, but they are also not public API fields by default. The read model exposes them behind admin authorization; the player DTO exposes only what a family or relationship view needs.

## Decision rule

If a proposed use of a trait cannot be explained in one sentence without implying a value judgement about a person, it does not belong in V4.
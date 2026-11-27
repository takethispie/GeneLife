# Targets for the Untargeted Bands

## Context

Three band-shaped quantities are reported but never asserted, so they cannot fail and cannot be checked: the company failure rate in the balance report, the 61 health warnings on the 2k ten-year run, and the 4 reproduction `Warn` levels on the 200-genos 30-year run. Each of them is currently prose in a STATUS file or a field in a written report, which is exactly the state the rest of this track exists to eliminate.

A band with no target is a report row, not an acceptance criterion.

## Design

For each quantity, set a target from measured behaviour rather than from a guess, and assert it at the scale where it is meaningful:

1. **Measure first.** Run the documented scenario and record the distribution and the current value, not just the endpoint. Health and reproduction warnings are event-shaped — characterise their rate and which categories dominate before choosing a number.
2. **Choose the target for the behaviour, not for the current value.** A target set just above today's number is a ratchet that hides the next regression. Where the correct target is "none at all" (unexplained warnings), the honest first move is to assert the count and fix the causes rather than to accept a ceiling.
3. **Assert at the scale the band is defined for.** Health warnings are a 2k ten-year figure; reproduction warnings are a 200-genos 30-year figure; company failures are reported at 200. Do not generalise a target across scales where the quantity is not measured.
4. **Record the rationale** next to the target — what the number means and why it is where it is — because a future reader must be able to tell a deliberate target from a tuned one.

## Tasks

- [ ] Characterise the health warnings: count, categories, whether they cluster in a life stage, and how the rate scales from 200 to 2k.
- [ ] Characterise the reproduction `Warn` levels the same way at 200 over 30 years.
- [ ] Decide and record a target for each of the three quantities, with the measurement behind it.
- [ ] Add assertions at the documented scale, with the scenario seeds already used by `BalanceScenarioTests`.
- [ ] Confirm the new assertions pass on master, and that deliberately breaking each one fails with a message naming the quantity.

## Files

- `Genelife.Performance.Tests/Scenarios/BalanceScenarioTests.cs`
- `docs/plans/roadmap-v7/STATUS.md`

## Contracts

- No existing band target changes.
- New assertions are added at the scale the quantity is defined for; they do not silently widen an existing band.
- Every target is recorded with its rationale in `STATUS.md`.

## Acceptance

- Each of the three quantities has a numeric target and an assertion that fails when the value moves outside it.
- The rationale for each target is written down where a reader will find it.
- `BalanceScenarioTests` is green on master with the new assertions in place.

# No Expected Reds

## Context

`BalanceScenarioTests` carried two comment blocks stating that rent burden and Gini "currently MISS the V5 bands" and instructing the reader to expect the assertion red until a rent-burden rebalance landed. The rebalance landed in `ede655b` and both bands went green, but the comments did not — nothing links a comment to the state it describes.

By the time it was caught, the file was telling a future reader to expect a failure that never arrives. That is worse than a missing comment: it converts the next genuine failure into "that old known issue".

The comments themselves are corrected in commit `558142d`. This step is about the class of defect, not the instance — the same drift already cost time in doc code references and in the snapshot DTOs.

## Design

Remove the tolerated-red pattern and make band state single-sourced:

1. **Forbid the annotation.** No assertion may be carried red with a comment explaining it. A band that cannot be met is a tracked work item, not a test that is green-adjacent.
2. **Put the state where it is checked.** Band values, targets and their rationale live in `STATUS.md` (the carry-in record) and in the assertion itself, so a reader has one place to look and a stale copy has somewhere to be corrected.
3. **Make the drift detectable.** Either assert the documented values where cheap, or run the doc reference checker over band narrative so a comment quoting a value that the suite contradicts is caught. Prefer the cheap mechanical check over a convention nobody enforces.
4. **Keep the rule that survived.** Assert against the target, never the measured baseline: a ratchet set just above a broken value hides a regression. That rule was right and is not what rotted.

## Tasks

- [ ] Grep the performance and domain suites for annotations announcing an expected failure and resolve each one: fixed, or deleted with a tracked work item.
- [ ] Confirm no band comment quotes a value the suite contradicts.
- [ ] Add the mechanical check chosen in step 3, or record explicitly that the state is single-sourced in `STATUS.md` and checked by review.
- [ ] Wire the check into the standard tier so it fails rather than warns.
- [ ] Record the rule in the `genelife` skill so it is not rediscovered.

## Files

- `Genelife.Performance.Tests/Scenarios/BalanceScenarioTests.cs`
- `Genelife.Domain.Tests/Docs/DocsConsistencyTests.cs`
- `docs/plans/roadmap-v7/STATUS.md`

## Contracts

- No assertion is relaxed to make a comment true.
- Deleting a knowingly-red assertion requires a recorded work item, not a silent removal.
- The check runs in the fast tier, not only on demand.

## Acceptance

- No comment or annotation in the suites describes an expected failure.
- No band narrative quotes a value the suite contradicts.
- The chosen check fails when a stale band narrative is reintroduced deliberately.

# Band Failure Messages

## Context

When the rent-burden band failed this cycle, reconciling the message with the evidence took a bisect and produced one wrong conclusion on the way. Two things made it hard:

- The message names the observed value and the target but not the **report file** the run just wrote, so there is nothing to open.
- The report holds a **per-year array** while the assertion reads the **final year** (`report.Years[^1]`). Reading `Years[0]` and comparing it against the message looks like a contradiction, and it is easy to do under pressure.

Diagnosability is part of the gate. A band that fails unhelpfully costs more than the failure.

## Design

Make every band failure self-describing:

1. **Name the band** in the message, not just the inequality — "median rent burden" rather than a bare number.
2. **Name the population and seed**, so the failing scenario can be re-run as a single command.
3. **Name the aggregate** explicitly (final year of the decade), so no reader has to guess which element of the array the assertion used.
4. **Name the report file** written by the run, so the evidence is one open away.
5. **Distinguish a broken band from a broken run.** Conservation failures (sink delta, conservation gap) are not balance drift and should not read as such.

Keep the existing rule intact: bands are asserted against the **target**, never against the measured baseline.

## Tasks

- [ ] Add a single helper that formats a band failure with band name, population, seed, value, target, aggregate and report path.
- [ ] Route both rent burden and Gini, and the `AssertTargetBands` set, through it.
- [ ] Include the report path (the scenario already writes `balance-<genoses>-<seed>.json`).
- [ ] Verify by deliberately breaking one band and reading the message as a newcomer would: can the cause be found without opening the test?
- [ ] Confirm the aggregate named in the message is the one the assertion actually used.

## Files

- `Genelife.Performance.Tests/Scenarios/BalanceScenarioTests.cs`

## Contracts

- No target changes.
- Failure messages gain information; no assertion becomes looser.
- The report path referenced in the message is the path the run actually wrote.

## Acceptance

- A deliberately broken band prints a message naming the band, population, seed, value, target, aggregate and report file.
- Re-running the named population and seed reproduces the failure without consulting the test source.
- The suite is green when no band is broken.

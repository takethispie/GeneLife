# E21 — Testing and acceptance

**Phase:** 13 — Verification
**Depends:** all economy steps
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Prove the economy is closed, deterministic, operable and within performance limits before housing and life stages build on it.

## Test layers

### Unit

- `Money` arithmetic and rounding.
- Payroll gross/net/tax/pension arithmetic.
- Pro-rata arrears and catch-up.
- Capacity and seat accounting.
- Ledger idempotency and conservation.
- Loan amortisation and delinquency.
- Solvency state machine.
- Revenue attribution weights.

### Integration

- Office → posting → application → hire → payroll → spending → revenue → payroll, over 10 cycles.
- Insolvent company: shortfall → arrears → restructuring → recovery.
- Bankrupt company: liquidation waterfall → employees separated → seats freed.
- Admin command → tick apply → read model reflects change.
- Snapshot save/restore with all economy relations.

### Scenario

A deterministic 10-year run with 1,000 sims and 10 companies, seeded. Assertions:

- money supply change within ±10%;
- unemployment within 4–10%;
- at least one company solvent at all times;
- no negative wallet from normal operations;
- total ledger debits equal credits;
- pension fund reconciles with statements;
- no orphan seats, employees or postings.

### Performance

Extend `PerformanceGuardrailTests`:

- 2,000 sims, 20 companies, 200 postings;
- p95 tick under the existing budget;
- economy systems allocate below a per-run ceiling;
- no Marten I/O on the tick thread (assert via a wrapped store that throws if touched).

## Tasks

- [ ] Add the unit suites listed above.
- [ ] Add an end-to-end economy scenario harness with a fixed seed.
- [ ] Add the 10-year guardrail test and store its metrics artifact.
- [ ] Add the performance guardrails and allocation ceilings.
- [ ] Add a ledger conservation test that runs after every scenario.
- [ ] Add failure-path tests for every admin command.
- [ ] Document how to reproduce a scenario from a seed in the plan README.
- [ ] Gate merge on `dotnet build`, `dotnet test`, and the scenario guardrail.

## Files

- `Genelife.UnitTests/Systems/*`
- new `Genelife.UnitTests/Economy/EconomyScenarioTests.cs`
- new `Genelife.UnitTests/Economy/LedgerConservationTests.cs`
- `Genelife.UnitTests/Performance/PerformanceGuardrailTests.cs`
- `performance-baselines/` (new economy baseline)

## Acceptance

- All new tests pass in CI.
- The 10-year scenario meets the target bands or fails with a clear metric diff.
- Ledger conservation holds in every scenario.
- Performance guardrail passes at 2,000 sims.
- No flaky test depends on wall-clock time or unseeded randomness.

## Testing

This step is the test plan. Its own acceptance is that the suite runs in under ten minutes locally and produces a machine-readable metrics artifact for comparison.

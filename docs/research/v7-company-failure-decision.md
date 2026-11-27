# Company failure rate — delete the band, or make bankruptcy happen?

**Author:** @pedro (Game Economy Specialist) · **Date:** 2026-11-27 · **Branch:** `docs/pedro/company-failure-decision`
**Answers:** card `t_afb391a1` (migrated from Vikunja #36) — *"Company failure rate band: 0.0 at every scale — delete or target?"*
**Updated:** card `t_a2351350` — the 0.02/yr lower bound: measured and retired as a non-target (§8)
**Builds on:** [`v7-benchmark-thresholds.md`](v7-benchmark-thresholds.md) §4.1 and §5.1 (same author-team, `837c856`)

---

## 1. Ruling

**Bankruptcy is intended. The band stays and is a work item — not a deletion.**

The design intent is explicit and first-party: V1's definition of done reads *"Companies earn revenue, pay
wages they can afford, and **can go bankrupt**."* (`docs/plans/roadmap-v1/README.md:188`), and the engine
carries the full machinery for it — a five-state solvency ladder, grace periods, a restructure pass, and a
priority-ordered liquidation waterfall (`CompanySolvencySystem`, `CompanySolvency`, `CompanyLiquidated`).
Nothing shells that machinery out, and several systems key off the `Bankrupt` state
(`JobPostingRepository:69`, `CorporateTaxSystem:45`, `PlayerActionService:1307`): a state that can never be
reached is dead code, and the band is measuring it.

So the correct terminal state for this card is **"work item"**, and the work is *make failure economically
reachable*, not *delete the band*. Deleting it would institutionalise a company life-cycle that cannot end —
the one thing an agent-based economy is supposed to produce.

**However, the band is not targetable yet**: the metric is wrong in two independent ways (§4) and the
economy is over-capitalised (§3). Fix the metric, then target it.

## 2. First-hand measurement (not a commit-message claim)

Run by me, this worktree, on the card's own scenario — seed 21, decade, Debug, filter
`/*/Genelife.UnitTests.Economy/BalanceScenarioTests/Ten_year_balance_report`. **4/4 green, exit 0.** Values
read from `Genelife.Performance.Tests/bin/Debug/net10.0/balance-reports/balance-<scale>-21.json`,
`Years[-1]`:

| Scale | Companies | `BankruptCompanies` | `CompanyFailureRate` | Company cash | Arrears (max over run) | Profit share y1 → y10 | Health warns |
|---:|---:|---:|---:|---:|---:|:--|---:|
| 60 | 5 | 0 | 0.0 | $4,421,392 | 0 | 0.125 → **−0.103** | 2 |
| 200 | 13 | 0 | 0.0 | $12,582,488 | 0 | 0.208 → 0.084 | 2 |
| 1k | 61 | 0 | 0.0 | $59,955,799 | 0 | 0.207 → 0.120 | 2 |
| 2k | 128 | 0 | 0.0 | $121,967,637 | 0 | 0.209 → 0.124 | 3 |

`BankruptCompanies` is **0 in every one of the 40 year-rows** (4 scales × 10 years), not just the final
year. `ArrearsCents` is **0 in every year-row at every scale**. The run reproduces
`v7-benchmark-thresholds.md`'s health-warning counts (2/2/2/3) and money-supply growth (0.0664–0.0830)
exactly, so this is the same economy that doc measured. Conservation is clean (`SinkDeltaCents` 0,
`ConservationGapCents` 0).

**Company cash barely moves across the decade** ($4.77M → $4.42M at scale 60, *−7 %*) even as the profit
share degrades to negative (−0.103 at scale 60, year 10). Bankruptcies do not merely fail to happen — the
companies never come close.

## 3. Root cause: the solvency ladder is never entered

The ladder (`CompanySolvencySystem.Evaluate`) needs one of two things to reach `Insolvent`:

1. a **defaulted loan** (`HasDefaultedLoan`, :56-57) — no company defaults on a loan at any scale; or
2. **`ArrearsCycles ≥ InsolvencyGraceCycles`** (:62-63) — arrears require payroll to be short
   (`PayrollSystem:197-201`), and arrears are 0 everywhere.

The other entry point is `Healthy → Tight`, taken when `account.Balance < account.ReservedForPayroll`
(:74), where `ReservedForPayroll` is set to **one pay cycle's payroll** every cycle
(`PayrollSystem:193`, `account.ReservedForPayroll = cashNeeded`). Company cash is ~$900k/company at 60
genoses against a monthly reserve well under $200k, so even `Tight` is not reached; `Tight → Healthy`
recovers immediately (it only stays `Tight` while `Balance < reserve` **and** arrears are positive).

The over-capitalisation is seeded deliberately: `EconomyOptions.SeededCompanyCapital = 1,200,000`
(:22, "enough working capital per worker for payroll to survive a decade"). That is exactly the
tension — the seed was sized to *survive* a decade, and it succeeds so completely that no company can
fail inside the gate's horizon. A 10-year runway relative to one-cycle obligations means the failure
mechanism is untested, not absent.

## 4. Two metric defects that must be fixed before a target is set

The band cannot be compared to any external anchor until both are corrected. They are independent.

**(D1) The reported rate is cumulative, not annual.** `BankruptCompanies` counts companies whose account
state *is* `Bankrupt` at snapshot time (`EconomyMetricsSystem:55-56`), and liquidation never removes the
entity (`CompanySolvencySystem:17` — "Liquidation never deletes the company entity, so history stays
available"). The stock is therefore monotonic over the run, and
`CompanyFailureRate = BankruptCompanies / companies` (`BalanceScenarioTests:259-263`) is a
**decade-cumulative failure ratio**, not the annual rate the external band (US ≈ 0.075/yr, OECD 0.02–0.15)
describes. `v7-benchmark-thresholds.md` §2 calls it "an annual rate" — that is incorrect; it compares a
10-year cumulative count against a 1-year rate. Fix before targeting: derive the annual flow from the
`CompanyLiquidated` event stream (or the year-over-year delta of the stock), divided by the company count.

**(D2) The health check is an absolute count; the band is a rate.** `EconomyHealth.Evaluate`
(`Genelife.Domain/Economy/EconomySnapshot.cs:80-85`) maps `BankruptCompanies` → `0 = Pass`,
`≤2 = Warn`, `>2 = Fail`. This is the "0 Pass / 2 Fail" the card asks to reconcile. A **fixed count** with
a **fixed threshold** means the check means opposite things at the two ends of the scale table:

| Scale | Companies | `>2` bankruptcy as a share |
|---:|---:|---:|
| 60 | 5 | **>40 %** |
| 2k | 128 | **>1.6 %** |

The same check is ~25× stricter at 2k than at 60 while reporting the same name. A rate band (0.02–0.15)
is scale-free; the count check is not. They are two metrics wearing one name.

## 5. Reconciliation — one quantity, one threshold

The fix is to make the health check and the acceptance band the **same quantity with the same thresholds**,
computed on the snapshotted economy:

1. **Correct the metric first (D1).** Report `CompanyFailureRate` as an annual rate: failures during the
   year ÷ companies. Until this is done, no threshold — count or rate — is comparable to anything.
2. **Carry it in the snapshot.** `EconomySnapshot` today has `BankruptCompanies` but no company count, so
   `Evaluate` *cannot* form a rate. Either add the annual failure flow + company count to the snapshot (the
   metric system already iterates `CompanyAccount`s and can count them in the same pass,
   `EconomyMetricsSystem:50-57`), or precompute the rate there. Prefer the latter — one derived quantity,
   no duplicated raw counter.
3. **Point the health check at it.** Replace the `switch (BankruptCompanies)` branch with the existing
   two-sided helper already in the same file —
   `Band("company-failure-rate", annualRate, lower, warnHigh, failHigh, "per year")`
   (`EconomySnapshot.cs:90-105`) — using the balance band's thresholds, so the check and the assertion can
   never drift apart again.
4. **Sequence the threshold change.** A two-sided band with a lower bound (`< 0.02 = Fail`) fires
   *immediately* on today's 0.0 — honest, but it would turn the gate red before the rebalance lands. Land
   the metric fix + the rebalance (§6) together, then switch the check to the two-sided band. Until then
   keep the check upper-bound-only so the gate is not knowingly red (`01-band-targets.md`: "a target set
   just above today's number is a ratchet").

Net: the absolute-count check **is deleted and replaced**, not merely retuned — but only after the metric
it reads is a rate. The band itself is **targeted**, after the mechanism is reachable.

## 6. Work items (handed off — not done in this card)

This card is the decision; the implementation is three separable changes. Ordered by dependency:

1. **Make company failure economically reachable** (rebalance, `@gabrielo` / `@pedro`, → `@main-dev` for
   tuning knobs). Levers, cheapest first: lower `SeededCompanyCapital`, or raise recurring company cost
   (`DefaultOfficeOperatingCost`, pension/tax), or let `Tight` persist long enough to accrue arrears —
   until a measurable minority of companies reach `Arrears`/`Insolvent` inside a decade. Acceptance: a
   non-zero `CompanyLiquidated` count at ≥2 scales on seed 21.
2. **Annualize the failure metric + reconcile the health check** (`@main-dev`, with the §5 design).
   Acceptance: the check emits the same level for the same *rate* at 60 and 2k; a test pins it.
3. **Assert the band at the documented scale** (`@gabrielo`, band guardrails). Acceptance: a numeric
   target on the annual rate with a failure message that names the band, scale, seed, observed value,
   target, report file and aggregate. Sequence *after* 1 and 2, else it asserts 0.0.

## 7. What this card does not settle

- **The target number.** The external annual firm-exit band is 0.02–0.15 (Fed FEDS 2020-089 ≈ 0.075;
  OECD ">10 % in many countries"). I endorse using it, but it cannot be asserted until work item 1
  produces a non-zero flow to measure, and work item 2 makes the flow annual.
- **Loan defaults.** No company defaults on a loan at any scale, so the `HasDefaultedLoan` entry into
  `Insolvent` is also dead. Whether that is by design is a separate question on the same ladder; flagged,
  not decided here.

**Resolution (card `t_a2351350`, §8).** The target number is now settled: the external **0.02/yr lower
bound is retired as an explicit non-target**, because no lever reaches it without breaking the
unemployment band. The band keeps its ceiling. See §8 for the measurements and the structural reason.

## 8. Resolution of the 0.02/yr floor: retired as a non-target (card `t_a2351350`)

Card `t_a2351350` was the follow-up work item 3 left open. §7 endorsed the external annual firm-exit
band **0.02–0.15/yr** but could not assert its lower bound, because the rebalance (work item 1) made
failure *reachable* without making it *common*: on the assert branch (`b0e6550`, seed 21, decade,
Debug, `BalanceScenarioTests`) the decade-mean annual exit rate is **0.0016–0.0167/yr** at 60/200/1k/2k,
1.2× to 12× below the floor.

This section closes that card. The floor is **not reachable by a rebalance**, so it is retired as an
explicit non-target. The band keeps its ceiling (0.15/yr warn, 0.30/yr fail) and its "mechanism is
live" assertion. The measurements behind that ruling follow.

### 8.1 Levers tried, measured (seed 21, decade, Debug, all four scales)

Every lever that moves the exit rate was run against the gate and read from
`balance-reports/balance-<scale>-21.json`. `unemp@200` is the binding constraint: the
`AssertTargetBands` unemployment ceiling is **≤ 0.10** and is asserted at 200. `failMean` is the
decade-mean annual rate from `MetricHistory`.

| lever | unemp@200 (≤0.10) | failMean@200 | failMean@1k | failMean@2k | verdict |
|---|---:|---:|---:|---:|---|
| **baseline** — office $8,000/cycle, seed capital $1.2M | 0.062 ✓ | 0.0071 | 0.0047 | 0.0016 | floor missed |
| office cost **$16,000**/cycle (2×) | **0.186 ✗** | 0.0067 | 0.0063 | 0.0056 | unemployment breaks first, floor still missed |
| office cost **$32,000**/cycle (4×) | **0.591 ✗** | 0.0133 | **0.0284 ✓** | **0.0222 ✓** | floor met — economy collapses: rent burden 0.49/0.56/0.63/0.52 (>0.40), vacancy 0.19/0.10/0.09/0.14, wage share 0.40–0.46 |
| seed capital **$400k** (3× shorter runway) | **0.298 ✗** | 0.0071 | **0.0227 ✓** | 0.0119 | unemployment breaks; money-supply growth 0.16–0.19 (>0.10) |
| office $32,000 **+ job growth 25 firms/yr** (8× the stabiliser) | **0.591 ✗** | 0.0133 | **0.0260 ✓** | 0.0193 | the entry channel does **not** decouple — unemployment unchanged at 0.60 |

Two further readings from the same runs:

- The failure rate is **clustered, not spread**. At every lever the exits land in the last 1–3 years
  of the decade; the office-cost lever is a *uniform* drain (at $32,000 the whole corporate sector
  runs from $11.1B to $0.4B over the decade), so it does not produce idiosyncratic churn — it
  produces a sector-wide run-down that only *looks* like a rate in the final years.
- A **30-year probe on unmodified defaults** (1k/2k) does not rescue the floor either: the full-run
  mean is **0.0176/yr** at 2k, still under the floor, and the years where the rate does exceed 0.02
  are years the economy is decaying (unemployment 0.20 → 0.74, rent burden 0.17 → 1.51, wage share
  1.04 → 0.26). The rate rises as the economy dies, not as it churns.

### 8.2 Why: a mechanism mismatch, not a tuning miss

`CompanySolvencySystem.Liquidate` fires every employee of the liquidated company (`EmployeeFired`)
and `HiringSystem` freezes hiring for companies that are not solvent. GeneLife's firms are the
**only** employer, and there is **no firm-entry mechanism** — only `JobGrowthSystem`, a capped
labour-market stabiliser (`JobGrowthMaxPerYear = 3`). So every exit permanently destroys jobs, and
the exit rate and the unemployment rate are rigidly coupled: driving ~20 % of the cohort out over a
decade drives unemployment to 0.19–0.60 against a ≤ 0.10 ceiling.

The external lower bound (0.02/yr) describes an economy with **continuous firm entry**, which is
what makes a ~2 %/yr exit rate sustainable there. GeneLife does not model entry, so the floor
asserts a property of a different economy. Per `01-band-targets.md` ("a target set just above
today's number is a ratchet"), the honest move is to retire the bound explicitly rather than to
retune the seed until a degenerate number clears it.

### 8.3 What is asserted instead

`BalanceScenarioTests.AssertCompanyFailureBand` keeps the two checks the sim can honestly make, at
every scale (60/200/1k/2k, seed 21):

1. **mechanism live** — the decade peak annual rate is non-zero, so a regression to the
   pre-`7298785` "no company can fail" state fails the gate; and
2. **ceiling** — the decade peak is within the `EconomyHealth` fail-high (0.30/yr) and the reported
   latest-year rate is within the warn-high (0.15/yr), using the same public constants as the health
   check so the assert and the check cannot drift.

`EconomyHealth.CompanyFailureLowerBound` **stays `0m`**: the check reads a single snapshot, and a
year with no exit is a normal year, so a 0.02 lower bound would emit `Fail` on almost every tick.

### 8.4 Structural work item (filed, not done here)

Reaching the external floor needs a **firm-entry / company-churn mechanism** — new firms founded
against profitable niches, so that exits are replaced and the labour market clears — not a tuning
knob. Filed as card **`t_2df92453`** (assignee `@main-dev`). Its acceptance: the decade-mean annual
exit rate reaches ≥ 0.02/yr at 1k/2k on seed 21 with unemployment ≤ 0.10 at 200, then flip
`CompanyFailureLowerBound` to `0.02m` and add the two-sided assertion this card did not add.

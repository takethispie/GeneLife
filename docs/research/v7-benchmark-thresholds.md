# V7 external benchmark thresholds for the three targetless bands

**Author:** @roberto (Solution Researcher) · **Date:** 2026-11-27 · **Branch:** `research/roberto/v7-benchmark-thresholds`
**Verified against:** `master` @ `837c856` · **Card (by title):** *"External benchmark thresholds for the targetless bands (company failure rate, health warnings, reproduction warnings)"*

**Scope decision (tech-leader, this session):** option **B** — source external references for the three bands that have
**no target at all**, with option **A** folded in as **Appendix A** (the already-green bands, as a sanity check on
`ede655b`). The card was re-scoped and retitled in `837c856`.

**Held line:** where no defensible published range exists, this document says so and proposes **no number**. A band
whose threshold is invented is worse than a band with no threshold, because it looks like a gate.

---

## 1. First-hand verification (not a commit-message claim)

I ran the gate myself before citing it, on a quiet machine (tech-leader confirmed the V6 perf window closed):

```bash
dotnet build Genelife.Performance.Tests/Genelife.Performance.Tests.csproj -c Release
dotnet run --project Genelife.Performance.Tests/Genelife.Performance.Tests.csproj -c Release --no-build \
  --no-launch-profile -- -class Genelife.UnitTests.Economy.BalanceScenarioTests -noColor
```

```
=== TEST EXECUTION SUMMARY ===
   Genelife.Performance.Tests  Total: 5, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 41.116s
```

**5/5 green, exit 0.** The parent commit was `837c856`; the run wrote four reports to
`Genelife.Performance.Tests/bin/Release/net10.0/balance-reports/balance-{60,200,1000,2000}-21.json`. Values below are
read directly from those files (final year, seed 21), **not** copied from the roadmap:

| Scale | Rent burden | Gini | Unemploy. | Vacancy | **Wage share** | Health warns | **Company failure rate** | Overcrowded units | Life exp. | **Replacement** | Bankrupt | Pop start→end | Money supply growth |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:--|---:|
| 60 | 0.2093 | 0.4998 | 0.0556 | 0.0741 | **1.103** | 2 | **0.0** | 0 | 72.9 | 0.000 | 0 | 60→60 | 0.0664 |
| 200 | 0.2917 | 0.5503 | 0.0559 | 0.0854 | **0.916** | 2 | **0.0** | 0 | 71.8 | 0.800 | 0 | 200→200 | 0.0792 |
| 1k | 0.3359 | 0.5683 | 0.0605 | 0.0705 | 0.880 | 2 | **0.0** | 9 | 76.6 | 0.926 | 0 | 1000→1001 | 0.0827 |
| 2k | 0.3446 | 0.5701 | 0.0768 | 0.0596 | 0.876 | 3 | **0.0** | 24 | 72.8 | 0.417 | 0 | 2000→1966 | 0.0830 |

Rent burden and Gini reproduce `ede655b`'s recorded figures exactly (0.2093/0.2917/0.3359/0.3446 and
0.4998/0.5503/0.5683/0.5701) — **the appendix-A sanity check passes**. Conservation is green in all four runs
(`SinkDeltaCents` 0, `ConservationGapCents` 0).

Two by-products of my own run, flagged as **observations from a single Release run, not reproduced**:

### 1b. Tail latency and the top hot spot (parsed from this run's own metrics log)

The engine logs one `[metrics]` line every 250 ticks. Before quoting numbers from it, the semantics matter — they are
**not** per-window figures (`Genelife.Application/Services/Metrics.cs`):

- `tick_ms p50/p95/p99` come from a **rolling ring buffer of the last 1024 tick durations** (`histogramCapacity = 1024`).
  Successive log lines therefore **overlap heavily** and are not independent samples — 55 lines is not 55 windows.
- the `top=[...]` figures are **`AverageMilliseconds` and `AverageAllocatedBytes` per call**, accumulated for the life
  of the `Metrics` instance (`new Metrics()` in `SimulationEngine.Initialize`, `Metrics.cs:292-305`), i.e. per
  simulation, not per window.

With that reading, my run says:

| Quantity | Value | Basis |
|---|---:|---|
| tick p95 | max **2.21 ms** across all 55 log lines | never over 50 ms — V7's "~5× headroom" is a **p95** claim and it holds |
| tick p99 | **17.55 ms** median, **168.94 ms** max; over 50 ms in **23 of 55** log lines | rolling 1024-tick buffer, overlapping samples |
| allocation | 3.23 MB/tick median | `alloc_per_tick` |
| **`RelationshipExposure`** | **~153 ms and ~288 MB per single call** (early-run averages ~250 ms / ~559 MB, diluted as the run proceeds) | top system by `AverageMilliseconds` in **55 of 55** log lines |
| `CompanyRevenue` | ~3.5 ms per call | two orders of magnitude smaller |

`RelationshipExposureSystem` is **monthly** (`Order 355`, `Cadence = TicksPerMonth`), so a decade at 2k genoses is
≈120 calls. Two consequences:

1. **The p99 tail has a named, single cause.** 120 calls in 12,000 ticks is ~1 % of ticks — exactly the p99 position of
   a 1024-tick buffer — so the p99 flips between the ordinary slow tick (~17 ms) and the monthly
   `RelationshipExposure` tick (~88-169 ms) depending on where the buffer boundary falls. The p99 is, in effect,
   measuring one system.
2. **The V6 allocation statement does not generalise.** `IMPLEMENTATION.md:105` says allocation "is dominated by the
   economy systems (`BasicIncome`, `ConsumerSpending`, `UtilityConsumption`)", but V6 measured through `LoadHarness`,
   whose fixture builds genoses **without `NetworkId` or `Traits`** (documented as a known deviation in that same
   file), so relationship work never executes there. In the balance run, where it does run, the monthly
   `RelationshipExposure` call's ~288 MB **matches the ~2.44 MB/tick × ~100 ticks/month ≈ 244 MB** the per-tick
   figure implies — i.e. that one system plausibly accounts for most of the monthly allocation. The economy systems
   are dominant *in a harness that cannot exercise relationships*: a fixture-coverage statement, not an economy one.

Caveat, stated: one Release run, one seed, one machine. The p95/p99 figures are the engine's own sampled values; the
per-call averages are cumulative and therefore dominated by the earliest (most expensive) calls. The direction and the
named cause are solid; the exact milliseconds are not a benchmark.

Also confirmed in this run: **`CompanyFailureRate` is identically 0.0** and `BankruptCompanies` is 0 at all four
scales over a decade. The band is not merely untargeted — **the metric is dead**, so any target would be met
vacuously (see §4).

## 2. What the three bands actually are (code truth, so targets attach to the right quantity)

**Company failure rate** — `Genelife.Performance.Tests/Scenarios/BalanceScenarioTests.cs:259-263`:
`BankruptCompanies` (final year, from `MetricHistory[^1]`) ÷ count of `CompanyName` entities. **An annual rate**, over
a decade-long run. Measured 0.

**Health warnings** — `BalanceScenarioTests.cs:253-258,317`: the count of checks at `Warn` **or** `Fail` from
`EconomyHealth.Evaluate(latest, previous)` + `HousingHealth.Evaluate(latest)` — twelve checks, each with its own
threshold already in code.

**Reproduction warnings** — `ReproductionInvariants.Verify(...)` (`Genelife.Domain/Family/ReproductionInvariants.cs`)
returns `Issue(Name, Level, Detail)`; **nine `Fail` kinds** (`pregnancy.parents`, `pregnancy.ticks`,
`pregnancy.children`, `pregnancy.duplicate-active`, `child.employed`, `child.pension`, `newborn.components`,
`newborn.parent-edges`, …) and **five `Warn` kinds** (`child.early-enrollment`, `child.not-enrolled`,
`child.no-household`, `housing.overcrowded`, `guardian.residence`). Counted in
`Genelife.Performance.Tests/Release/FamilyReleaseScenarioTests.cs:132`.

### The "61 health warnings" number is not the balance report's number

V7's STATUS/README carry **61 health warnings (2k, 10 years)** from V4 STATUS. That number comes from a **different
harness with a different filter**: `FamilyReleaseScenarioTests` counts `status.Health.Count(check => check.Level ==
"Warn")` — **Warn only, at one instant** — whereas the balance report counts **Warn + Fail at the final year** over two
evaluators. My own run reports **2 / 2 / 2 / 3** at 60/200/1k/2k.

So "61" and "3" are both correct for what they measure, and **nothing in the balance report reproduces 61**. A board
number that its own gate cannot reproduce is exactly the record rot V7 track 1 describes.

## 3. External reference values

### 3a. The three bands themselves

| Band | Value (mine) | Published reference | Source | Verdict |
|---|---|---|---|---|
| Company failure rate | **0.0** at every scale | US annual **firm** exit ≈ **7.5 %** of firms (2015-2018); establishment deaths ≈ **8.5 %/yr**; **"business death rates exceed 10 % per year in many countries"**; employer-enterprise churn ≈ **10-20 %** (industry) to **15-30 %** (services); ~half of new firms do not survive 3 years | Fed FEDS 2020-089 (BLS BED / Census BDS); OECD *Entrepreneurship at a Glance* 2016/2017; OECD GOV/RDPC/TI(2017)2 | **Targetable as a rate** — but see §4: at 0.0 the sim is ~7.5-10 pp below real churn, so a target cannot be met "as-is" in either direction |
| Health warnings (count) | 2 / 2 / 2 / 3 | — | — | **No defensible published range.** The *count of an application's own internal checks in Warn* has no external analogue. Do not invent one — anchor the **underlying checks** instead (§3b) |
| Reproduction warnings (count) | 4 `Warn` (reported, 30-y, not re-verified here) | — | — | **No defensible published range** for invariant-violation counts. The demographic *signals* they proxy do have references (§3b) |

### 3b. External anchors for the checks and report rows behind those bands

| Quantity (GeneLife) | Value (mine) | Published reference | Source | Comment |
|---|---|---|---|---|
| Unemployment rate | 0.0556 – 0.0768 | Global **5.0 %** (2023), **4.9 %** projected 2024; steady at **5 %** in 2024 | ILO WESO May 2024 Update; WESO Trends 2025 | Sim is within ~0.6-2.8 pp of the global rate — plausible |
| **Wage share** | **0.876 – 1.103** | Global **labour income share 52.4 %** (2024), 53.0 % (2014) | ILO WESO May 2025 Update | Sim runs **1.7-2.1×** the real labour share, and crosses its own `Fail` line (>0.90) at 60/200 |
| Housing vacancy rate | 0.0596 – 0.0854 | US **rental** vacancy **6.9 %** (Q4 2024); 6.6 % Q2, 6.9 % Q3 | US Census HVS Q4 2024 | Sim is inside the real range — the ≤0.12 band has real headroom above the ~7 % norm |
| Median rent burden | 0.2093 – 0.3446 | **30 %** of income = the affordability standard; **40 %** = HUD voucher lease-up cap; **>50 %** = "severely cost burdened" | HUD Cityscape 20(1); CRS R49457 | The 0.40 ceiling sits exactly on HUD's lease-up cap — **the existing band is externally defensible**, not arbitrary |
| Overcrowded units | 0 – 24 units (0 % – 1.5 %) | EU **16.8 %** (2023) / **16.9 %** (2024) / ~**17 %** (2025) of *population* in overcrowded dwellings | Eurostat *Housing in Europe* 2024/2025; Living conditions | **Unit mismatch**: Eurostat measures *population share* with a rooms-per-household definition, GeneLife counts *units*. A number here would be false precision |
| Wealth Gini | 0.4998 – 0.5701 | World Bank: **average Gini 38.2** across 165 countries; **>40 = "high inequality"**; US **41.8** (2023) | World Bank PIP / World Bank Policy Research WP 10796 | The **0.70 ceiling is ~1.8×** the World Bank high-inequality line; the sim sits in the 0.4-0.5 "high disparity" band |
| Life expectancy | 71.8 – 76.6 | Global **73.3 years** (2023), 73.4 in 2019; Europe 79.1; world 73.2 | WHO Global Health Estimates 2023; OWID | Sim brackets the global figure — **a good real anchor for a band that currently has none** |
| Population "ReplacementRate" | 0.000 – 0.926 | Replacement fertility = **TFR 2.1** (global TFR **2.25** in 2024; 55 % of countries below 2.1) | UN *World Population Prospects 2024* | **Definitions differ**: GeneLife's field is `births / deaths` (`PopulationMetricsSystem.cs:152`), not a TFR. Only the *stationarity* reading (≥1.0 = not shrinking) maps onto 2.1. Do not equate them |
| Money supply growth | 0.0664 – 0.0830 /yr | US **M2** long-run average **6.8 %/yr** (1960-2019, median 6.7 %); 2024 ≈ 1.5 %, 2025 ≈ 4.1 % | FRED series M2SL (via published summaries) | Sim growth (~6.6-8.3 %) sits just above the long-run real average — good agreement |

## 4. Proposed targets, and the ones I refuse to propose

1. **Company failure rate — targetable, but fix the metric first.** External band: **annual firm exit rate 0.02-0.15**
   (US ≈ 0.075; OECD "> 10 % in many countries"). I **refuse** to hand this over as an acceptance target yet,
   because at a measured **0.0** the sim has an economically degenerate company life-cycle: a green band there proves
   nothing. The spine needs one question answered first — *is company bankruptcy intended to happen?* If yes, the
   mechanism is dead and the target is a work item; if no, the band should be **deleted**, not targeted.
2. **Health warnings — no count target.** Propose instead: **(a) `Fail` count = 0 at every scale** (already true where
   checked), **(b) every check threshold carries an external citation or is explicitly marked sim-specific**, using
   §3b, and **(c) the count is reported with its harness named**, so the 61-vs-3 confusion cannot recur.
3. **Reproduction warnings — no count target.** Propose **(a) Fail count = 0**, **(b) triage the five `Warn` kinds
   individually** against their own criterion (e.g. `housing.overcrowded` against the Eurostat *population* measure
   with the unit mismatch stated, not against a unit count), and **(c)** keep them out of the balance gate until each
   has a criterion.
4. **New bands with real external anchors** (cheap, and they close "no target" gaps V7 already lists): **life
   expectancy** (73.3 y global ± a stated tolerance) and **money supply growth** (≤0.15/yr warn is already inside the
   real 6.8 % average — the internal threshold is defensible as-is).

## 5. Findings beyond the card (reported to owners, **nothing changed**)

1. **The `bankrupt-companies` health check is an absolute count, not a rate** (`EconomyHealth.Evaluate`:
   `0 → Pass`, `≤2 → Warn`, `>2 → Fail`) — while the band with no target *is* a rate. At 2k (128 companies) one
   failure warns and three fail; the same check on the same economy at 60 companies means something entirely
   different. This is the direct link between this card and the targetless rate band. **Owner: main-dev / pedro.**
2. **Wage share is structurally far from the external anchor** (sim 0.876-1.103 vs ILO 52.4 %) and is **asserted only
   at the 200 scale** — at 60 it measures **1.103**, past the check's own `Fail` line (>0.90). Either the metric's
   denominator is not comparable to a labour share, or the economy is mis-calibrated. **Owner: pedro.**
3. **`PopulationSnapshot.ReplacementRate` returns an arbitrary constant when `deaths == 0`**
   (`PopulationMetricsSystem.cs:152`: `deaths > 0 ? births/deaths : births > 0 ? 1.5m : 1m`) — a reported metric that
   silently emits 1.5 or 1.0 with no data behind it. **Owner: main-dev.**
4. **Record rot, second instance**: the health-warning figure on the board (61) cannot be reproduced by the balance
   report (3 at 2k). Same defect class as the "EXPECT THIS RED" comments fixed in `558142d`. **Owner: tech-leader.**
5. **The perf tail has a named cause.** p95 never exceeds 2.21 ms, but p99 exceeds the 50 ms budget in 23 of 55 log
   lines (max 168.94 ms), and the system behind it is **`RelationshipExposure`, at ~153 ms and ~288 MB per single
   monthly call** — top system in 55/55 lines. It also means V6's "allocation is dominated by the economy systems"
   came from a harness whose fixture cannot run relationship work at all. Numbers and the semantics of the metrics
   line in §1b. **Owner: main-dev**, because the headroom claim V7 carries is a p95 claim.

## 6. What I could not source (stated, not papered over)

- **No published benchmark exists for "count of internal health checks in Warn/Fail"**, nor for "count of app-specific
  invariant `Warn`s". The two candidate targets in §4 are therefore *structural* (0 Fail, cite the thresholds), not
  numeric — I did not manufacture a number to fill the gap.
- **No external analogue for GeneLife's `ReplacementRate`** as defined (`births/deaths`); the 2.1 replacement-fertility
  standard is a **TFR**, a different quantity. Stated rather than mapped.
- **Overcrowding** is not comparable without a unit conversion (Eurostat population-share vs GeneLife unit-count).
- The **61 / 4 V4 numbers** are taken from V4 STATUS and were **not re-verified** by me: reproducing them needs the
  release harness (`FamilyReleaseScenarioTests`), which is outside this card and heavy. Reported as reported-not-verified.

---

## Appendix A — the already-green bands vs published ranges (sanity check on `ede655b`)

| Band (target) | Sim value (my run, seed 21) | Real-world range | Reading |
|---|---|---|---|
| Rent burden ≤ 0.40 | 0.2093 / 0.2917 / 0.3359 / 0.3446 (60/200/1k/2k) | 30 % standard; >40 % lease-up cap; >50 % severe (HUD) | Green **and inside** the defensible band; ceiling lands on HUD's cap |
| Gini ≤ 0.70 | 0.50 / 0.55 / 0.57 / 0.57 | world average 38.2; >40 = high inequality (World Bank) | Passes the sim's ceiling but sits **above** the real high-inequality line — the 0.70 ceiling is loose by ~1.8× |
| Vacancy ≤ 0.12 | 0.0596 / 0.0854 / 0.0705 / 0.0741 | US rental vacancy 6.6-6.9 % (2024, Census HVS) | Green and realistic |
| Unemployment ≤ 0.10 | 0.0556 / 0.0559 / 0.0605 / 0.0768 | global 4.9-5.0 % (ILO) | Green; 0.0768 at 2k is ~2.7 pp above the global rate |
| Pension funding 0.80-2.00 | 1.000 at every scale | — | Passes, but pinned exactly at 1.000 at all scales — worth a look for a hidden clamp |
| Money supply growth ≤ 0.10 (decade) | 0.0664-0.0830 | M2 long-run 6.8 %/yr (FRED M2SL) | Green and realistic |

Appendix conclusion: **`ede655b`'s green bands agree with published ranges** for rent burden, vacancy, unemployment and
money-supply growth. The two places where the sim diverges from the external world are **Gini** (0.57 sim vs 0.38 world
average / >0.40 high-inequality line) and **wage share** (0.876-1.103 sim vs 0.524 labour income share) — both are
*passing* bands today, which is why they are findings for pedro rather than failures.

## Sources

**Repo (verified at `837c856`)** — `Genelife.Performance.Tests/Scenarios/BalanceScenarioTests.cs` (bands, counters,
report shape); `Genelife.Performance.Tests/Release/FamilyReleaseScenarioTests.cs` (61-warning count basis);
`Genelife.Domain/Economy/EconomySnapshot.cs` (`EconomyHealth`, 5 checks + thresholds);
`Genelife.Domain/Housing/HousingSnapshot.cs` (`HousingHealth`, 7 checks + thresholds);
`Genelife.Domain/Family/ReproductionInvariants.cs` (9 Fail / 5 Warn kinds);
`Genelife.Application/Systems/PopulationMetricsSystem.cs:152` (`ReplacementRate = births/deaths`, dead-branch constants);
`docs/plans/roadmap-v7/{README.md,STATUS.md}`; `docs/plans/roadmap-v4/STATUS.md` (61 / 4);
commits `ede655b`, `558142d`, `837c856`, `5ddc300`.

**External** — [Fed FEDS 2020-089, *Business Exit During the COVID-19 Pandemic*](https://www.federalreserve.gov/econres/feds/files/2020089pap.pdf)
(firm exit ≈ 7.5 %/yr, establishment ≈ 8.5 %/yr, citing BLS BED and Census BDS);
[BEA/FESAC, Decker, *Measuring business exit*](https://apps.bea.gov/fesac/meetings/2021-12-10/Decker.pdf);
[OECD, *Entrepreneurship at a Glance 2017*](https://www.oecd.org/content/dam/oecd/en/publications/reports/2017/09/entrepreneurship-at-a-glance-2017_g1g7f9d7/entrepreneur_aag-2017-en.pdf)
and [OECD GOV/RDPC/TI(2017)2](https://one.oecd.org/document/GOV/RDPC/TI(2017)2/en/pdf);
[UN *World Population Prospects 2024 — Summary of Results*](https://desapublications.un.org/file/20622/download) (TFR 2.25, replacement 2.1);
[WHO Global Health Estimates — life expectancy 73.3 y (2023)](https://www.who.int/data/gho/data/themes/mortality-and-global-health-estimates/GHO/)
([OWID: world 73.2](https://ourworldindata.org/grapher/life-expectancy));
[ILO WESO May 2025 Update](https://www.ilo.org/sites/default/files/2025-05/WESOUpdate_May2025_1.pdf) (labour income share 52.4 %);
[ILO WESO May 2024 Update](https://www.ilo.org/media/550996/download) and [WESO Trends 2025](https://www.ilo.org/sites/default/files/2025-01/WESO25_Trends_ExecSum_EN.pdf) (unemployment 4.9-5.0 %);
[US Census HVS Q4 2024](https://www.census.gov/housing/hvs/files/qtr424/Q424press.pdf) (rental vacancy 6.9 %);
[HUD Cityscape 20(1)](https://www.huduser.gov/Portal/periodicals/cityscpe/vol20num1/ch2.pdf) and [CRS R49457](https://www.congress.gov/crs-product/R49457) (30/40/50 % rent-burden thresholds);
[Eurostat *Housing in Europe* 2024](https://ec.europa.eu/eurostat/web/interactive-publications/housing-2024) and [Living conditions in Europe — housing](https://ec.europa.eu/eurostat/statistics-explained/index.php?title=Living_conditions_in_Europe_-_housing) (overcrowding 16.8-17 %);
[World Bank Policy Research WP 10796](https://openknowledge.worldbank.org/server/api/core/bitstreams/5bbdfcc2-d3c5-499e-9b6e-75bec14a1cb7/content) (average Gini 38.2; >40 = high inequality) and [World Bank PIP / OWID](https://ourworldindata.org/grapher/economic-inequality-gini-index);
[FRED M2SL](https://fred.stlouisfed.org/series/M2SL) (M2 long-run average 6.8 %/yr);
[PLOS ONE, basic macroeconomic ABM](https://journals.plos.org/plosone/article/file?id=10.1371/journal.pone.0277615&type=printable) and
[Delli Gatti et al., *Financial fragility, industrial dynamics and business fluctuations in an agent-based model*](https://opus.lib.uts.edu.au/bitstream/10453/9932/1/2008004078OK.pdf) (bankruptcy-as-exit modelling in calibrated ABMs).

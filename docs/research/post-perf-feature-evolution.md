# After the performance roadmap — what shipping sim/agent teams build next

**Author:** @roberto (Solution Researcher) · **Date:** 2026-11-27 · **Branch:** `research/roberto/post-perf-feature-evolution`
**Verified against:** `master` @ `937644b` · **Card:** *"Prior-art brief: post-performance-roadmap feature evolution (V7 input)"* (`t_e25790bb`)

**Companion document:** [`v7-benchmark-thresholds.md`](v7-benchmark-thresholds.md) answers *what number goes in the three
targetless bands*. This document answers a different question: **what do teams that ship agent / sandbox simulations
build once the performance roadmap closes, and who owns a band?** The two are meant to be read together; neither
duplicates the other.

**Scope line.** Per the product-owner ruling of 2026-11-27, performance work is out of scope until the Godot desktop
client reaches parity with the browser client. This brief contains **no performance recommendation and no performance
acceptance criterion**. The V6 numbers appear exactly once, in §1, as the premise the card handed me — cited to the
roadmap, not re-argued.

---

## 1. The premise, as measured rather than asserted

V6 left roughly **5× headroom**: the 2,000-genos gate runs p95 **1.97 ms** and the 5k synthetic fixture **10.8 ms**,
both against a **50 ms** budget, with allocation flat; and both performance opt-ins died on second contact — cadence
staggering measured as noise in Release, and parallel per-entity passes were deleted for leaking foreground threads.[1]

The roadmap's own reading of that result is the sentence this brief takes as its starting point: *"Another performance
roadmap would be inventing work."*[1]

So the question is not "what do teams optimise next". It is: **when the simulation already runs fast enough, what is
the remaining risk, and what do shipping teams build against it?** Across the four groups below the answer is
consistent, and it is the work V7 track 1 already names:

| | What changes when scale stops binding | What gets built |
|---|---|---|
| Risk moves from | "the tick does not fit the budget" | "the emergent macro behaviour is wrong, or right and we cannot tell" |
| Deliverable moves from | a harness and a profile | an **external reference**, a **named owner**, a **cadence**, and a **reproducible artifact** |
| Failure mode moves from | dropped ticks | a band nobody owns, a record that rots, a red nobody can reproduce |

---

## Part A — What shipping simulation teams build after the performance roadmap

### A1. Agent-simulation labs: scale first, then validation of the emergent behaviour

The progression is legible in the literature. Smallville simulated 25 agents; Project Sid ran **10 – 1000+** agents in a
persistent Minecraft world and evaluated them against *civilizational* benchmarks — autonomous role specialisation,
collective rules, cultural and religious transmission — rather than against a frame rate.[2] The interesting part for
V7 is what the benchmark *is*: a set of macro-level behavioural claims about the society the agents produced, not a
property of the engine.

The clearest statement of the shift is Park et al. 2024, *Generative Agent Simulations of 1,000 People*. Its headline
number is a **validation** result: interview-grounded agents replicated their real counterparts' General Social Survey
answers *"85% as accurately as participants replicate their own answers two weeks later"*.[3] Two things in that
sentence matter here:

1. the agents are checked against an **external reference** (real people's answers), and
2. the result is **normalised by the reference's own noise floor** (human test–retest reliability) instead of being
   reported as a bare accuracy.[3]

AgentSociety follows the same shape at larger scale — 10k LLM agents, 5M interactions — and validates by reproducing
four real-world social experiments (polarization, inflammatory-message spread, universal basic income, hurricane
shocks), reporting *"alignment between AgentSociety's outcomes and real-world experimental results"*.[4] Concordia
splits the responsibility explicitly: a **Game Master** owns the environment and the grounding of every variable, and
the paper is candid that generative agent-based models sit on a **hierarchy of evidence** with weaker rungs below the
strongest one.[5][6]

**Read for V7.** For a sandbox with emergent macro behaviour, the post-scale deliverable is *a validation set with an
external reference and a stated evidence level*. That is precisely the three bands V7 lists as having no target at
all — company failure rate, health warnings, reproduction warnings — and it is why "no target" is a real gap rather
than a tidiness issue.

### A2. Shipped sandbox economies: they build an economy-reporting function with a named owner

This is the closest analogue to V7 track 1, because these teams have already solved scale and are now managing a live
emergent economy.

**EVE Online** is the strongest case. CCP Games hired a Lead Economist in 2007[10] and published Quarterly Economic
Newsletters containing the money supply, faucets and sinks, a Consumer Price Index and mineral/producer price
indices.[7][8] The first issue states plainly that *"the economic system of EVE Online has been described as a 'sink
and faucet' system"*, reports about **90 trillion ISK** in the universe, and gives daily gross faucets of **536 billion**
against gross sinks of **365 billion** — i.e. it reports *closure*, not just level.[7] The economist's stated job was
*"determining whether there are sufficient 'sinks' to absorb excess currency should an inflationary bubble start to
form"*.[10] When the QEN series was retired it was replaced by a **Monthly Economic Report**, announced as *"filling the
information gap that was created when the Quarterly Economic Newsletters were discontinued"*.[9]

**Eco** (Strange Loop Games) ships the measurement inside the product: *"All the data generated from the interaction of
plants, animals, climate, and players can be searched and analyzed using in-game graphs and heat-maps"*, and the
studio's own design writing frames simulation data as something players *"can cite as evidence in debates"*.[13][14]
Note the division of labour: the studio owns the **measurement**, and player-made institutions (laws, taxes, elections)
own the **intervention**.[13][14]

**The design canon agrees.** Lehdonvirta & Castronova's *Virtual Economies: Design and Analysis* (MIT Press, 2014)
introduces faucets and sinks as the building blocks of a virtual macroeconomy, and its recommended management method
is to *"measure or estimate how much money your faucets create in a period of time … and compare that with your
measure or estimate of how much money is erased by the sinks during the same period"* — the difference being the net
change per capita.[11][12] It also recommends **regular reporting of the Gini** as a standing early-warning signal
rather than a one-off study.[11]

**Read for V7.** Economic closure is not a milestone you reach once; in shipped economies it is a **recurring report
with a named owner and a cadence**, checked against an external reference. GeneLife already has the report
(`balance-{60,200,1000,2000}-21.json`) and the gate that reads it.[1] What it does not have is the owner column, the
review cadence, or an external reference for the rows that have none.

### A3. Deterministic sandbox engines: they build the artifact that makes a failure reproducible

**Factorio** shows what the *diagnosability* half of this work looks like. The game is deterministic-lockstep, and when
a desync happens it produces a **desync report** — log file, reference save, desynced save and replay — precisely so
that someone else can reproduce it.[16] FFF #158 then describes the fix for the report's main weakness: the server
checks **save-load stability locally** so that *"this report will not contain any accumulated changes from running
different version of the save, so only the original cause of the problems will appear in the diff"*.[15] Determinism
work was still paying out years later: FFF #415 documents a threading determinism bug introduced in 2017 and found
only when four independent conditions came together on machines with different core counts.[17]

**Read for V7.** A band failure that "costs a bisect" is the same problem Factorio solved, and the fix has two halves:
the message must **name** what failed (band, population, seed, observed, target, aggregate), and the failure must
**ship its artifact** (the report file), so the reader is not reconstructing state by hand. V7 track 1 already lists
the message half as a deliverable; the artifact half is the part prior art adds.

---

## Part B — How emergent-economy simulations anchor macro metrics

### B1. Pattern-oriented modelling: many patterns, several scales, and independent predictions

Pattern-oriented modelling (POM) is the established framework for exactly GeneLife's problem — accepting a bottom-up
model whose interesting behaviour is emergent. Grimm et al. 2005 define it as using *"multiple patterns observed in
real systems at different hierarchical levels and scales … systematically to optimize model complexity and to reduce
uncertainty"*, and state that *"the gold standard for model validation, or for theory in general, is to make
independent or secondary predictions"*.[18][23] The JASSS editorial on ABM grand challenges restates it: POM *"allows
us to compare model output to multiple, instead of a single, patterns, observed at different scales and levels of
organization"*, and *"each pattern is used to reject unrealistic process representations or parameter combinations"*.[19]

**Read for V7.** V7's structure — bands asserted at 60 / 200 / 1k / 2k — is already POM-shaped, and that is a point in
its favour worth recording. The gap POM names is the word *independent*: every band is currently asserted on the same
scales and seeds that the `ede655b` rebalance was tuned against.[1] **A held-out scale or seed is the cheapest
available defence against a gate that has been calibrated into passing**, and it is a single assertion, not a
roadmap.

### B2. Stylized facts are the acceptance set — and calibration is not validation

Macro-ABM practice accepts a model against a **set** of stylized facts. Windrum, Fagiolo & Moneta describe validation
as *"examining the extent to which the output traces generated by a particular model approximates one or more
'stylised facts' drawn from empirical research"*, and their four-step indirect-calibration procedure starts by
identifying the stylized facts and then building the model with micro behaviour kept as close to evidence as
possible.[20] Fagiolo & Roventini select among models by *"the number of stylized facts each of them is able to jointly
replicate"*, and note that ABMs can target facts *"at different level of aggregation (i.e. micro vs. macro
regularities)"*.[21]

The warning is equally explicit. The *Towards a Validation Methodology for Macroeconomic Agent-Based Models* paper
describes the replication-of-stylized-facts approach as treating the model as valid if it reproduces the set — and
notes that in that approach *"all of the model parameters are calibrated indirectly, meaning that they are quantified
such that the model is able to reproduce the set of stylized facts"*.[22]

**Read for V7.** This is the external grounding for a rule V7 already states in
`01-economic-closure/summary.md`: *"bands are asserted against the target, never against the measured baseline,
because a ratchet set just above the current value hides a live regression"*. The literature's version of the same
point is that a fact you calibrated to is not evidence of structural realism — which is why B1's held-out check
matters, and why the three untargeted bands must not simply be given today's measured value.

### B3. The honest limit: for an open system you *confirm*, you do not *validate*

The paper V7's "held line" is ultimately downstream of is Oreskes, Shrader-Frechette & Belitz (1994):

> "Verification and validation of numerical models of natural systems is impossible. This is because natural systems
> are never closed and because model results are always non-unique. Models can be confirmed by the demonstration of
> agreement between observation and prediction, but confirmation is inherently partial. … Models can only be evaluated
> in relative terms, and their predictive value is always open to question. The primary value of models is
> heuristic."[25]

The JASSS social-ecological survey reports the same state of affairs from the modelling side: *"Verification of
social-ecological ABMs in the sense of comparing model results to real-world data or patterns is still in its
infancy"*.[23]

**Read for V7.** This is the citable basis for refusing to invent a threshold. Where no defensible external range
exists — and for *"count of an application's own internal checks in Warn"* none does — the correct move is a
**structural** criterion (fail-count zero, every underlying threshold carrying its own citation or an explicit
"sim-specific" marker) plus a statement of the evidence level, not a number that looks like a gate. The sibling
document already takes that position; this brief supplies the reason it is the *standard* position rather than a local
preference.

The **ODD protocol** (second update, 2020) is the other half of the same discipline: a model description must be
reproducible, must point readers at the relevant model code, and the update explicitly added sections on **rationale
and evaluation**.[24] **Read for V7:** the band narrative *is* an ODD-style artifact. It must name the code path and
the evaluation, and — per §C3 — it must be kept true by a mechanism, not by good intentions.

### B4. Report the trade-off, not a single optimum

The AI Economist frames tax design as a two-level RL problem and reports an **equality–productivity trade-off**,
improving it by 16% over the Saez baseline rather than claiming an optimum.[26]

**Read for V7.** A coupled band set (rent burden ↔ vacancy ↔ Gini ↔ housing pressure) is a trade-off surface, which is
what V7's STATUS already says: *"a change that fixes one band can push another out"*, with `ede655b` as the worked
example. The acceptance criterion that follows is "the **pair** is inside both bands", not "one metric is optimal" —
and the `ede655b` note that neither of its two fixes passes the bands alone is a direct instance of the frontier
shape the AI Economist reports.

---

## Part C — Band ownership: keeping an acceptance record from rotting

### C1. Specification limits versus control limits — assert against the target, never the observed value

The NIST/SEMATECH handbook draws the line precisely: *"Control Limits are used to determine if the process is in a
state of statistical control (i.e., is producing consistent output). Specification Limits are used to determine if the
product will function in the intended fashion."*[27] The FDA's SPC guidance is blunter about the consequence of
mixing them: specifications placed on a control chart read as process goals, which *"undermines the efficacy of SPC
procedures"* for detecting change.

**Read for V7.** This is the outside literature's name for V7's own rule. A threshold copied from the current measured
value is a **specification limit computed from process data** — it cannot detect the regression it exists to detect,
because the process defines it. So: the target is a spec (rent burden ≤ 0.40, Gini ≤ 0.70, vacancy ≤ 0.12 …), the
measured baseline is not, and the gate asserts the spec.[1][27] The three untargeted bands cannot be "targeted" by
promoting today's value to a constant.

### C2. Every band needs a named owner and a review cadence

SRE practice is the mature form of this. An SLO target *"needs to be owned by someone in the organization who is
empowered to make tradeoffs between feature velocity and reliability"*, and the SLO record names its authors,
reviewers and approvers — because an objective that belongs to everyone belongs to no one.[28] The error budget is
what turns the target into a standing decision rather than a number on a page.[29]

Riot's **Champion Balance Framework** is the game-industry version: explicit numeric bands per audience, published so
that players *"can hold the 'balance' team accountable"*, and a follow-up update that shows bands being **tightened and
loosened deliberately with the reason stated** (pro bands loosened because the diversity gains did not justify the
nerf volume; average/skilled bands tightened by 0.5%).[30][31] Riot also says out loud that the framework is *"not a
hard set of rules that we must follow, as there are a ton of edge cases"* — the band is a decision rule with a named
owner, not a law of nature.[31]

**Read for V7.** The band table in `roadmap-v7/STATUS.md` has Band / Population / Target / Value / Source columns. It
has **no owner and no review cadence**, and nothing records *why* a target has its value or when it last changed. The
prior art says those three additions — owner, cadence, recorded change — are the difference between an acceptance
criterion and a report row.

### C3. Record rot is a known failure mode; the known fix is to make the record executable

Martraire's *Living Documentation* is the reference practice: documentation should evolve at the same pace as the
code and be produced or verified by automation, so that a failure *"signals the documentation is no longer in sync
with the code"* and can be fixed immediately.[34] Mechanically that is documentation tests — Rust runs code samples
from doc comments as tests by default, and compiles examples by default explicitly *"to protect them from
bit-rotting"*.[35]

GeneLife already has the pattern in miniature: `DocsConsistencyTests` asserts that documentation names real paths and
real routes, and the verification plan runs that guard on **every change under `docs/`**.[1]

**Read for V7.** The two record-rot instances V7 track 1 lists — `BalanceScenarioTests` carrying "EXPECT THIS RED"
comments for a red that ended at `ede655b`, and the board's 61-health-warning figure that the balance report cannot
reproduce — are the same defect class as a doc test that is never run.[1] The prior-art-consistent fix is to convert
the claim into an assertion where the gate can also produce the number, and to **name the harness** where it cannot.
Writing a more careful comment is the one remedy the literature does not recommend.

### C4. A known-red or known-off state needs an owner *and* an expiry, or it becomes permanent

Google's flaky-test mitigation is the canonical quarantine: a tool monitors flakiness and, when it is too high,
*"automatically quarantines the test. Quarantining removes the test from the critical path and files a bug for
developers to reduce the flakiness. This prevents it from becoming a problem for developers, but could easily mask a
real race condition or some other bug in the code being tested."*[32]

Fowler's feature-toggle practice is the same shape for switches: toggles *"come with a carrying cost"*; *"Savvy teams
view their Feature Toggles as inventory which comes with a carrying cost, and work to keep that inventory as low as
possible"*; some teams add a removal task the moment a toggle is created, others put **expiration dates** on toggles,
and *"some go as far as creating 'time bombs' which will fail a test … if a feature flag is still around after its
expiration date"*.[33]

**Read for V7.** V7's rule — *"Every 'still opt-in' marker must end V7 as either shipped or deleted, with the reason
recorded"* — is Fowler's inventory rule.[1][33] What is missing is the **time bomb**: nothing in the tree fails when an
opt-in outlives its roadmap, and `CohabitOnPairing` has now survived V4, V5 and V6 and is entering V7.[1] That is not
an argument that the team is careless; it is the empirical demonstration that a rule without a guard does not hold.

### C5. A metric nobody acts on must be deleted

Alert hygiene states the rule directly: *"Delete Alerts Nobody Has Acted On in 6 Months"* — if an alert fired and no
one acted, it is noise — and the reason given is the one that matters here: *"an alert nobody acts on is worse than no
alert, because it trains people to ignore the next one"*.[38][39] Every alert carries an owning team, and the test is
whether the responder can name the owner and the first action.[39] Experimentation guardrails use the same
construction from the other direction: a guardrail is a metric with a **threshold *and* an action** — *"Block full
rollout if any guardrail breaks"*.[36][37]

**Read for V7.** A band with no target and no owner is the reporting analogue of a page nobody acts on. Health warnings
and reproduction warnings currently fire without a threshold and without a follow-up, which is exactly the "trains
people to ignore the next one" failure — and it is the mechanism by which a real red gets waved through later. Either
give each a threshold and an action, or move it out of the gate and keep it as a diagnostic with its harness named.

---

## Part D — Mapping onto V7 track 1, decision by decision

Each row is a decision the spine has to make, the prior art that bears on it, and a recommended action. Owners are
suggested from the existing board record; the routing decision is @tech-leader's.

| # | Decision | Prior art | Recommended action | Suggested owner |
|---|---|---|---|---|
| **D1** | Company failure rate: target it or delete the band? | A metric that is identically 0 is a dead mechanism; POM/Oreskes order is *verify the mechanism, then confirm the rate*[18][25]; the external range already exists in the sibling doc | Verify whether bankruptcy is **reachable** first. If yes → the mechanism is the work item and the target is the external range. If no → **delete the band**, do not target it[25] | pedro / main-dev |
| **D2** | Health warnings and reproduction warnings have no target | No external analogue exists for counts of an application's own checks; the standard response is a structural criterion plus a stated evidence level[23][25] | Convert to structural criteria — **Fail count = 0**, each underlying threshold cited or explicitly marked sim-specific — and name the **harness** in the report so the 61-vs-3 confusion cannot recur[1] | pedro |
| **D3** | Who owns a band? | An objective needs an owner empowered to trade off, plus a review cadence; band changes should be recorded decisions, not silent constant edits[28][30][31] | Add **Owner** and **Review cadence** columns to the `roadmap-v7/STATUS.md` band table, and record every band change as *who / why / from → to* | tech-leader |
| **D4** | The band narrative rots | Living documentation: make the record executable so drift fails a build[34][35]; GeneLife's own `DocsConsistencyTests` is the local precedent[1] | Assert the numbers the gate can also produce; **name the harness** for any number it cannot. No narrative figure that the gate can check should live only in prose[1] | gabrielo (docs guard) |
| **D5** | A band failure costs a bisect | A failure needs both a **naming message** and a **self-contained artifact**[15][16] | Failure message names band, population, seed, observed value, target, report file and aggregate — and the report file is treated as the failure's artifact, as Factorio's desync report is[15][16] | pedro |
| **D6** | Is the gate calibrated into passing? | POM's gold standard is an **independent** prediction; a fact you calibrated to is not evidence of structural realism[18][19][22] | Hold out **one scale or seed** from the rebalance set and assert the bands there. One assertion; it is the cheapest structural-realism check available[18][19] | pedro |
| **D7** | The two carried opt-ins | Long-lived toggles are inventory with a carrying cost; the practice that holds is an expiry with a **time bomb** that fails a test[33]; quarantine needs an owner and an exit[32] | Delete (or ship) both with the reason recorded, **and** add a guard that fails when a new opt-in outlives its roadmap. The rule already exists; the guard does not[1][33] | main-dev |
| **D8** | Coupled bands: what is "passing"? | Coupled objectives are a trade-off frontier, not a single optimum[26]; V7's own STATUS makes the same coupling point[1] | Accept on the **pair** being inside both bands, and record which lever moved which band — `ede655b` is the worked example to generalise[1][26] | pedro |

**What this brief does *not* ask for.** No new harness, no new metric, no new scale. D3, D6 and D7 are the only items
that add anything at all, and each is one column, one assertion and one guard respectively.

---

## What does not transfer

Stated explicitly, so the analogies are not over-read:

- **EVE and Eco have a live player base generating the data.** GeneLife's economy is closed and synthetic, so
  "publish a report for players" has no direct analogue. What transfers is the **owner + cadence + external
  reference**, not the audience.[7][14]
- **Riot's bands are statistical** (win rate across millions of games) and move with a meta; GeneLife's bands are
  structural targets with a spec. The transferable part is the governance — a published band, a named owner, a
  recorded change — not the numbers or the thresholds.[30][31]
- **LLM-agent labs validate against human data.** GeneLife has no human reference for its economy, so the transferable
  ideas are the *normalisation* convention (Park's 85% is relative to human test–retest reliability) and the explicit
  evidence level, not the accuracy figure itself.[3][6]
- **The ABM literature is mostly about scientific models whose purpose is explanation.** GeneLife is a product, so
  Oreskes' *"the primary value of models is heuristic"* is a caution against over-claiming, not a licence to skip the
  gate.[25]
- **Factorio's artifact works because the simulation is deterministic and replayable.** GeneLife has determinism and
  replay tests in its invariant set, so the pattern applies — but only if the report file is genuinely sufficient to
  reproduce the run.[1][15]

---

## Verdict

1. **The pattern is real, and V7 track 1 is it.** Across agent-simulation labs, shipped sandbox economies and
   deterministic engines, the work that follows a closed performance roadmap is *acceptance of emergent behaviour*:
   an external reference, a named owner, a cadence, and an artifact that makes a failure reproducible.[2][7][15]
   That is a strong signal that track 1 is correctly chosen as the spine, not a tidy-up.[1]
2. **The highest-value additions this prior art implies, and the current definition lacks, are two: an owner +
   review cadence on every band (D3), and one held-out scale or seed asserted as an independent prediction
   (D6).** Both are cheap, both address a failure the present definition cannot catch, and D6 is the only structural
   defence against a gate that has been calibrated into passing.[18][19][28]
3. **Refuse to invent a number for the two count bands.** No published analogue exists for a count of an
   application's own internal checks, and the modelling literature's own answer to that situation is a structural
   criterion plus a stated evidence level, not a plausible-looking threshold.[23][25] The sibling document's held line
   is the standard position, not a local preference.
4. **Company failure rate is the one untargeted band with a real external range — and it is the one where the order
   matters.** Verify the mechanism before validating the rate; targeting a measured 0.0 would be a specification limit
   computed from process data, which is the exact failure mode §C1 names.[18][25][27]
5. **Keep the "assert the spec, not the baseline" rule and record *why*.** It is the single place where the current
   definition already matches the external literature, and NIST's control-limit/specification-limit distinction is the
   citation that makes it durable.[27]
6. **The opt-in rule needs a time bomb, not a rule.** `CohabitOnPairing` surviving V4 → V7 is the evidence that the
   rule alone does not hold; Fowler's expiry-and-time-bomb practice is the fix.[1][33]
7. **No performance recommendation is made here, and none should be inferred.** The 5× p95 headroom is cited once as
   the premise[1]; the product-owner ruling that performance is out of scope until the Godot client reaches parity
   stands, and nothing in Parts A–D turns on a perf number.

---

## Sources

[1] https://github.com/takethispie/GeneLife — GeneLife repository (docs/plans/roadmap-v7, docs/research)
[2] https://arxiv.org/abs/2411.00114 — Project Sid: Many-agent simulations toward AI civilization (Altera, 2024)
[3] https://arxiv.org/pdf/2411.10109v1 — Generative Agent Simulations of 1,000 People (Park et al., 2024)
[4] https://arxiv.org/html/2502.08691 — AgentSociety: Large-Scale Simulation of LLM-Driven Generative Agents (Piao et al., 2025)
[5] https://deepmind.google/research/publications/64717 — Concordia: generative agent-based modeling (Google DeepMind)
[6] https://arxiv.org/html/2312.03664 — Concordia paper: actions grounded in physical, social or digital space
[7] http://cdn1.eveonline.com/community/QEN/QEN_Q3-2007.pdf — EVE Online Quarterly Economic Newsletter Q3 2007
[8] https://content.eveonline.com/community/QEN/QEN_Q2-2010.pdf — EVE Online Quarterly Economic Newsletter Q2 2010
[9] https://www.eveonline.com/news/view/eve-economy-update-eve-vegas-2015-report — EVE Economy Update - EVE Vegas 2015 (Monthly Economic Report)
[10] https://www.reuters.com/article/technology/virtual-world-hires-real-economist-idUSN09256192 — Virtual world hires real economist (Reuters, CCP Games)
[11] https://mitpress.mit.edu/9780262535069/virtual-economies — Lehdonvirta & Castronova, Virtual Economies: Design and Analysis (MIT Press)
[12] https://aaltodoc.aalto.fi/server/api/core/bitstreams/8ffd4078-668f-4bb0-89f2-f897309e1375/content — Virtual Economy Design in MMORPGs (Aalto University thesis)
[13] https://strangeloopgames.com/worlds-of-consequence — Strange Loop Games: Worlds of Consequence (Eco design)
[14] https://store.steampowered.com/app/382310/Eco — Eco on Steam (in-game graphs, heat-maps, citable simulation data)
[15] https://www.factorio.com/blog/post/fff-158 — Factorio Friday Facts #158 - desync report improvement
[16] https://factorio.com/blog/post/fff-188 — Factorio Friday Facts #188 - Bug, Bug, Desync
[17] https://factorio.com/blog/post/fff-415 — Factorio Friday Facts #415 - deterministic multithreading
[18] https://todd.bendor.org/upload/Grimm%20et%20al%202005.pdf — Grimm et al. 2005, Pattern-Oriented Modeling of Agent-Based Complex Systems (Science)
[19] https://www.jasss.org/23/1/13.html — JASSS editorial: Meeting Grand Challenges in Agent-Based Models
[20] https://jasss.org/10/2/8/8.pdf — Windrum, Fagiolo & Moneta 2007, Empirical Validation of Agent-Based Models (JASSS)
[21] https://www.jasss.org/20/1/1.html — Fagiolo & Roventini, Macroeconomic Policy in DSGE and Agent-Based Models Redux (JASSS)
[22] https://link.springer.com/article/10.1007/s10614-021-10191-w — Towards a Validation Methodology for Macroeconomic Agent-Based Models
[23] https://www.jasss.org/20/2/8.html — Agent-Based Modelling of Social-Ecological Systems (JASSS)
[24] https://www.jasss.org/23/2/7.html — Grimm et al. 2020, The ODD Protocol - second update (JASSS)
[25] https://sites.tufts.edu/models/files/2019/03/oreskes-verification.pdf — Oreskes, Shrader-Frechette & Belitz 1994, Verification, Validation and Confirmation (Science)
[26] https://arxiv.org/abs/2004.13332 — The AI Economist: Improving Equality and Productivity with AI-Driven Tax Policies
[27] https://www.itl.nist.gov/div898/handbook/pmc/section3/pmc32.htm — NIST/SEMATECH e-Handbook 6.3.2 - control limits vs specification limits
[28] https://sre.google/workbook/implementing-slos — Google SRE Workbook - Implementing SLOs
[29] https://sre.google/sre-book/embracing-risk — Google SRE Book - Embracing Risk (error budgets)
[30] https://www.leagueoflegends.com/en-au/news/dev/dev-champion-balance-framework — Riot Games /dev: Champion Balance Framework
[31] https://www.leagueoflegends.com/en-us/news/dev/dev-balance-framework-update — Riot Games /dev: Balance Framework Update
[32] https://testing.googleblog.com/2016/05/flaky-tests-at-google-and-how-we.html — Flaky Tests at Google and How We Mitigate Them
[33] https://www.martinfowler.com/articles/feature-toggles.html — Martin Fowler - Feature Toggles (aka Feature Flags)
[34] https://www.infoq.com/articles/book-review-living-documentation — Q&A with Cyrille Martraire on Living Documentation (InfoQ)
[35] https://dev-doc.rust-lang.org/stable/cargo/commands/cargo-test.html — The Cargo Book - cargo test (documentation tests)
[36] https://absmartly.com/blog/experimentation-guardrails — Experimentation Guardrails (ABsmartly)
[37] https://venue.cloud/news/insights/scaling-experiments-the-booking-com-way — Booking.com: Scale Experiments Effectively (guardrails with thresholds and actions)
[38] https://openobserve.ai/blog/alert-fatigue — Alert Fatigue: Fix Noisy Alerts with SLO-Based Alerting
[39] https://www.alittlebit.tech/alert-fatigue-actionable-observability-signals — Alert Fatigue: How to Design Actionable Observability Alerts

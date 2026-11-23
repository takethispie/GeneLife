# Population continuity

A population can grow from immigration, from births, or from both. Today it grows only from immigration. That makes the age pyramid a policy output: the target population is set, arrivals fill it, and the number of children is whatever the generator seeded.

Births change the shape of continuity. They add dependants before they add workers, so the dependency ratio moves first and the labour force follows years later. Immigration adds working-age adults immediately. A healthy population needs both, and the simulation should show the difference.

## Target population as a floor

The existing immigration target should be a **floor**, not a cap:

- If the living population is below target, immigration and births both help close the gap.
- If births push the population above target, immigration pauses; the population is allowed to grow.
- If deaths push it below target, immigration resumes subject to the existing unemployment, housing and working-age checks.
- The target is not silently raised to match a baby boom; it is a policy floor with visible growth above it.

## Why not one-for-one replacement

A simple “one birth cancels one immigrant” rule is easy to write and wrong to reason about:

- a newborn is not a worker for 18 years;
- an immigrant arrives as an adult with skills and housing needs;
- the age pyramid, dependency ratio and pension base differ sharply.

The policy should compare **population gap**, **working-age share** and **birth cohort**, not just a headcount.

## What the operator should see

- births, deaths and arrivals in the same yearly snapshot;
- natural growth vs immigration growth;
- children as a share of the population;
- the working-age share and old-age dependency ratio;
- whether immigration is paused and why;
- the population target and the current gap.

## What success looks like

- The population does not collapse when immigration pauses.
- Births contribute a visible share of replacement over a decade.
- The age pyramid has children, workers and retirees at the same time.
- The labour market and pension system can absorb the cohort.
- Money and housing remain stable while the population changes.

## What V4 does not do

- No pronatalist policy target that overrides individual state.
- No immigration shutdown as a population strategy.
- No claim that births alone must replace the population in every run.
- No player-facing demographic dashboard in V4a.
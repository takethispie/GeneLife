# Release and learning

V4 changes population continuity, household composition, inheritance and the money loop at the same time. A release is not just “the tests passed.” It is a controlled transition that preserves old worlds and leaves enough evidence to tune the next version.

## Release principles

1. **Migrate, do not reset.** Existing worlds keep their sims, money and housing; traits and family state are backfilled deterministically.
2. **Persist the rules.** Options and coefficients are stored with the save so a later deploy does not silently change behavior.
3. **Make the change visible.** Births, pairings, inheritance and migrations emit events and appear in the admin.
4. **Keep the balance loop honest.** The 10-year scenario and the conservation gap must be green before and after.
5. **Document the misses.** Wealth Gini, vacancy and company failures were already outside target; V4 should not pretend they are fixed.

## What to measure

- births, deaths, arrivals and natural change;
- replacement rate and dependency ratio;
- partnership rate and average age gap;
- candidate counts and attraction score distribution;
- orphan and state-care counts;
- minor trust total and conservation gap;
- household size and overcrowded units;
- tick p95 and snapshot size at 2,000 sims;
- projection lag.

## Rollout

1. Run the migration on a copy of a real save.
2. Compare the family graph, money supply and population before/after.
3. Run 10 years on the migrated copy.
4. Check the family page and the health checks.
5. Roll out with the admin-only page; no player-facing family UI in V4a.
6. Keep the previous build available for rollback.

## What can go wrong

- A default that changes old saves silently.
- A migration that invents parent edges or loses money.
- A pairing system that pairs relatives or minors.
- A birth path that creates an incomplete sim.
- A trust that is not counted in money supply.
- A projection that lags so far the admin sees a different family.

Each has a mitigation in the acceptance suite. The release checklist exists to make sure the mitigations actually ran.

## Learning after release

- Compare the family decade report with the pre-V4 baseline.
- Review the trait distribution and mutation rate after three generations.
- Review pairing rejection reasons and age gaps.
- Review child costs and arrears by household.
- Decide whether the next version needs adoption, divorce, a richer genome or player-facing family UI.

## What not to do

- Do not tune the birth rate by changing a target band.
- Do not hide a defect by deleting the invariant.
- Do not ship the ML model into the tick loop to “improve” a score that is already explainable.
- Do not add a player-facing family feature before the admin can audit it.
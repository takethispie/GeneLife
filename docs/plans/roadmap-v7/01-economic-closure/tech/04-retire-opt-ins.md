# Retire the Opt-Ins

## Context

Two features have been configurable and unshipped for two roadmaps. Each is tested in neither state, so its cost is invisible and its risk unpriced:

- **`CohabitOnPairing`** — introduced in V4, carried through V5. The 30-year 200-genos comparison shows population 199 -> 219 and births 75 -> 83, but vacancy 17.7% -> 26.6% and overcrowded units 3 -> 9. It has been "pending economy/consumption recalibration" since V4.
- **Cadence staggering (`StaggerCadence`)** — introduced in V6. Re-measured in Release this cycle: 2,000-genos gate p95 1.97 ms with offsets zeroed against 2.72 ms enabled, deltas within the repeat-to-repeat spread, allocation unchanged to within ~50 B per tick. No gain, and it moves the balance bands when enabled (0.4369 against a 0.40 ceiling).

An opt-in that outlives two roadmaps is unpriced risk. Either outcome — shipped or deleted — is acceptable. "Still opt-in" is not.

## Design

Resolve each on its own terms, and record the reason:

1. **Cadence staggering: delete the switch, keep the decision.** The measurement is decisive and already recorded in `roadmap-v6/IMPLEMENTATION.md`. Remove the opt-in path so the codebase has one cadence behaviour, and leave the decision record intact — including that it was measured in Release, not inferred.
2. **`CohabitOnPairing`: ship it or delete it, on evidence.** If it ships, the vacancy and overcrowding numbers must first land inside band, which is economy work in this same track. If it is deleted, drop the recalibration and say so. Do not leave it as a switch that nobody has run in two roadmaps.
3. **Deletion is a real option.** A feature that has never been enabled in production ranks below the features that are; if the recalibration is not worth doing now, deleting the switch and keeping the branch history is the honest close.

## Tasks

- [ ] Delete the `StaggerCadence` opt-in path and its wiring; confirm the harness environment switch used for the Release measurement is removed or repurposed.
- [ ] Re-run the release tier after removal to confirm the single cadence behaviour is the measured-off behaviour and no band moved.
- [ ] Decide `CohabitOnPairing` on evidence: either bring vacancy and overcrowding inside band and enable it, or delete it and record why.
- [ ] If it is enabled, re-run the 30-year comparison and record the new numbers against the V4 baseline.
- [ ] Record both decisions, with reasons, in `../STATUS.md` and in the V4/V6 status pages that currently carry the opt-in note.

## Files

- `Genelife.Application/Services/SimulationEngine.cs`
- `Genelife.Domain/Housing/HousingOptions.cs` and the relationship options type carrying `CohabitOnPairing`
- `Genelife.Performance.Tests/Performance/LoadHarness.cs`
- `docs/plans/roadmap-v4/STATUS.md`, `docs/plans/roadmap-v6/STATUS.md`, `docs/plans/roadmap-v7/STATUS.md`

## Contracts

- Cadence behaviour after removal matches the measured-off behaviour; no band baseline moves.
- No "opt-in" marker survives in `docs/plans/` for either feature.
- If `CohabitOnPairing` is enabled, its vacancy and overcrowding figures are inside band before the switch defaults on.

## Acceptance

- Zero surviving opt-in markers for `CohabitOnPairing` and cadence staggering.
- The reason for each outcome is recorded where a reader will look, including which measurement decided it.
- The release tier and the balance bands are green after the changes.

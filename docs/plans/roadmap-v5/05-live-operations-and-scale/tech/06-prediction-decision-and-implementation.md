# L06 — Prediction decision and implementation

**Phase:** 6 — Client
**Depends:** L04, V3 interpolation, player action latency data
**Estimate:** 2–3 sessions
**Status:** Done (V5 feature 05) — no-prediction decision recorded

## Goal

Make an evidence-based decision about own-player prediction and implement it only if the measured latency justifies the complexity.

## Context

V3 uses interpolation with a 120 ms delay and no own-player prediction. The V3 status lists prediction as a deferred decision. V5 adds more player actions and a live preview, so perceived responsiveness matters, but prediction adds reconciliation, rollback and edge cases.

The decision must be measured, not assumed.

## Design

### Measurement

- Record input-to-ack latency, intent admission time, server tick age and interpolation delay.
- Record perceived movement delay on the reference hardware and network.
- Compare keyboard/pointer and touch input.
- Capture snap/reconciliation frequency if a prototype exists.
- Record the player's subjective rating in a short playtest.

### Decision gate

Implement prediction only if:

- the measured delay is above the agreed threshold for a meaningful share of sessions;
- the cost is bounded to local movement, not economy, family or social actions;
- reconciliation can snap cleanly without ghosting;
- the no-prediction baseline is preserved as a fallback.

Otherwise, document the measurement and keep interpolation.

### If prediction is implemented

- Predict only the local player's movement from the last acknowledged state.
- Reconcile against the server position; snap on a threshold or after a viewport change.
- Never predict wallet, job, housing, family or social results.
- Keep a feature flag and a measured rollback path.
- Add a visual error metric and a test for reconciliation.

### If prediction is not implemented

- Record the latency report and the decision.
- Keep the interpolation settings tunable.
- Add a regression test for the no-prediction path.
- Revisit after the live preview data.

## Tasks

- [x] Add input-to-ack and perceived-latency instrumentation.
- [x] Run the measurement on the preview and reference hardware.
- [x] Write the go/no-go decision with evidence.
- [ ] If go, implement local movement prediction and reconciliation.
- [x] If no-go, document the baseline and keep interpolation.
- [ ] Add a feature flag and rollback path.
- [ ] Add tests for snap, reconnect and viewport change.
- [x] Record the decision in `STATUS.md` and release evidence.

## Resolution

Decision: **no prediction**. Pointer input-to-ack p95 was 51 ms. Touch outliers
(109–603 ms in emulation) coincided with a `Pathfinding` max of 1.15–1.47 s and
tick p99 of 50–335 ms, so they are a server tick-stall symptom. Prediction would
not fix a stalled tick and would add reconciliation complexity without evidence
that corrections are frequent. The interpolation settings stay tunable, the
no-prediction path is the existing tested path, and touch is a profiling trigger
for the P12 scaling work. A subjective perceived-delay playtest was not run; it is
listed as an exception in the release evidence.

## Files

- `clients/browser/src/simulation/interpolation.ts`
- new `clients/browser/src/simulation/prediction.ts` (if implemented)
- `clients/browser/src/net/*`
- `clients/browser/docs/performance.md`
- `docs/plans/roadmap-v5/STATUS.md`

## Acceptance

- The latency report covers keyboard, pointer and touch.
- The decision is recorded with numbers, not opinion.
- Any prediction is limited to local movement.
- Reconciliation snaps cleanly on threshold, reconnect and viewport change.
- The no-prediction path remains available.
- Economy, family and social results are never predicted.
- The release evidence includes the decision.

## Testing

- Measure the baseline on the preview.
- If implemented, test prediction, reconciliation, snap and reconnect.
- Assert no predicted wallet/family/social result.
- Assert the flag off path is unchanged.
- Run a 30-minute soak with prediction on and off.

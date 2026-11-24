# Prediction decision (L06)

**Decision: no client-side prediction in V5. Keep the 120 ms interpolation buffer.**

The existing loop is input → hub intent → tick admission → snapshot delta →
interpolation. Prediction was only justified if measured input-to-ack was high
enough that the interpolated avatar visibly lagged behind the click, or if
reconciliation corrections were frequent. Neither was observed in the V5
preview smoke run, so prediction would add a second position authority without
evidence that it improves play.

## Measurement

Instrumentation is now part of every browser session:

- `clients/browser/src/net/latency.ts` records input-to-ack round trips for every
  admitted or rejected intent (200-sample window, p50/p95/max).
- `IntentClient` reports `onLatency` on the synchronous result and on an
  asynchronous `IntentRejected`.
- The HUD exposes `Input ack` and the debug overlay prints
  `input ack p50 … p95 …`.
- The threshold to revisit prediction is **p95 ≥ 100 ms** in the preview
  environment, or **more than 5% of own-player moves showing a correction**.

Preview acceptance run (`E2E_LIVE=1`, local API + Vite, seeded 200-sim world,
5 moves at 220 ms spacing):

| Metric | Value |
|---|---|
| Pointer input-to-ack p50 | 40 ms |
| Pointer input-to-ack p95 | 51 ms (5 samples) |
| Touch input-to-ack p95 | 109 ms single tap (emulated Pixel 7); 149–603 ms across repeats |
| Test | `clients/browser/e2e/live-acceptance.spec.ts` and `e2e/mobile.spec.ts` |

The pointer sample is small and the touch sample is unstable. During the touch
repeats the server reported a `Pathfinding` max of 1.15–1.47 s and tick p99 of
50–335 ms, so the high touch p95 was a tick stall, not admission or network RTT.
Client prediction cannot fix a tick that has already stalled; it would only hide
the stall until reconciliation. Touch latency is therefore a profiling trigger
for the P12 scaling work, not evidence for a movement-prediction rewrite.

## Why not predict now

- The current pointer admission p95 (51 ms) is below one server tick plus the
  existing 120 ms interpolation delay, so prediction would not remove the
  dominant visual delay.
- The touch outliers are caused by a slow tick (`Pathfinding` seconds-long max),
  not by input delivery. The follow-up is to profile that path first.
- One touch session is not a meaningful share of sessions; the gate requires
  repeated preview data before a prediction prototype is justified.
- Prediction requires client reconciliation for movement rejection, collision,
  building entry and death; a wrong prediction is more visible than the current
  controlled lag.
- There is no measured correction problem to solve. Adding prediction without
  telemetry evidence would violate the feature’s own acceptance criterion.

## Revisit checklist

1. Collect HUD p95 and correction counts from the preview deployment.
2. If p95 ≥ 100 ms or corrections > 5%, prototype prediction behind a setting
   for movement only; keep server state authoritative.
3. Re-run the [live acceptance latency test](../../../../clients/browser/e2e/live-acceptance.spec.ts)
   and compare p95 and perceived correction, not only frame rate.
4. If the prototype cannot reconcile without rubber-banding, keep interpolation
   and reduce the tick budget or network latency instead.

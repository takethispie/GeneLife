# H03 — Performance, soak and observability

**Phase:** 3 — Quality
**Depends:** R05, T06, H02
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Measure the client at the target scale, catch leaks and jank, and expose enough diagnostics to identify the failing layer without developer tools.

## Context

The target is 2,000 NPCs, a 256×256 map and 32 players. Browser performance varies widely; budgets must be tied to a named reference machine and scenario.

## Design

### Budgets (initial)

| Metric | Budget |
|---|---|
| Frame p95 | ≤ 16.7 ms on reference desktop |
| Map decode | ≤ 100 ms for 256×256 |
| First map visible | ≤ 2 s local |
| Snapshot apply | ≤ 2 ms for 2,000 sims |
| Bytes per client | Record and trend; alert on regression |
| Heap growth | No unbounded growth over 30 min |

### Soak

- Run 30 minutes with a synthetic or live world.
- Pan/zoom periodically; force two reconnects.
- Watch heap, WebGL context, sprite pool size and listener count.
- Verify no ghost sprites or growing maps.

### Diagnostics

Show or log:

- FPS, frame p95, renderer type.
- Map id/version, snapshot tick, last applied tick.
- Entity counts, visible counts, sprite pool size.
- Bytes received per second and per event type.
- Interpolation delay/buffer depth.
- Reconnect count, last error, rate-limit events.
- Build version.

Never log tokens or full player records.

## Tasks

- [ ] Add the synthetic 2,000-sim and map fixtures.
- [ ] Add a performance harness and named reference scenario.
- [ ] Add the 30-minute soak script/checklist.
- [ ] Add client metrics to the debug overlay and structured logs.
- [ ] Verify no listener/sprite/map leak after reconnect.
- [ ] Record baseline numbers in the roadmap status.

## Files

- new `clients/browser/src/test/perf/*`
- new `clients/browser/src/ui/debug-overlay.ts`
- new `clients/browser/docs/performance.md`

## Acceptance

- Frame, decode, apply and first-map budgets are measured and recorded.
- A 30-minute soak shows no unbounded growth.
- Diagnostics name the layer for a failed scenario.
- No sensitive data appears in logs.
- Regressions are visible in CI or the release checklist.

## Testing

- Perf harness.
- Soak run.
- Leak checks.
- Diagnostic snapshot.

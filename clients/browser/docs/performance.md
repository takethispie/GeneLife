# Browser client performance

## Budgets

| Path | Budget | Enforcement |
|---|---|---|
| RLE decode, 256×256 map | < 150 ms at startup | `src/test/performance.test.ts`, `rle-decoder.test.ts` |
| Apply a 2,000-sim snapshot | < 200 ms | `src/simulation/store.test.ts` |
| Interpolate 2,000 sims | < 16.7 ms | `src/test/performance.test.ts` |
| Full-map tile pass (65,536 tiles) | < 60 ms | `src/test/performance.test.ts` |
| HUD updates | ≤ 10 Hz | `src/ui/hud.ts` (100 ms interval) |
| Viewport requests | debounced 200 ms, ≥ 1 tile threshold | `ViewportSynchronizer` |
| Snapshot frame buffer | 1 s / 64 frames | `SimulationStore` |

The 16.7 ms budgets cover the frame-critical work the client owns. They do not
measure GPU fill rate.

## Measured locally

- 256×256 fixture: 668 KiB JSON, decodes into a `Uint8Array(65,536)`; unit tests
  enforce the startup budget.
- 2,000-sim fixture: 1.4 MiB JSON; snapshot apply and first frame copy are
  covered by the store budget test.
- Live initial full snapshot in a seeded dev world: ~283 KiB for ~600 sims.
- Headless Chromium (SwiftShader, 1280×720) rendered ~43 FPS with 76 viewport
  sims and ~1,025 visible tiles, and 25–30 FPS with a 592-sim legacy snapshot.
  Hardware-accelerated desktop Chromium is the target environment; the headless
  numbers are a floor, not a GPU benchmark.
- Tile presentation data is cached per decoded map generation; only the visible
  band is touched per frame.

Reproduce the frame probes with the fixture and live Playwright specs and the
`DebugOverlay` (F3). The overlay reports FPS, frame EMA, visible tiles, buildings,
sims, tick, buffer depth, interpolation delay and receive rate.

## How data flows

1. SignalR callbacks validate payloads, mutate `SimulationStore`, record one
   position frame per tick, then acknowledge the applied tick.
2. The Pixi ticker asks the store for an interpolated render state at
   `latestTick + elapsed - interpolationDelay` (never past the latest tick).
3. The camera computes the visible tile rectangle. Tile, building and sim layers
   acquire sprites from pools for visible entries only.
4. The same rectangle is converted to a viewport request, debounced and sent as
   `SetViewport`. `ViewportResult` is treated as authoritative.

## Tuning

- `GAME_CONSTANTS.defaultInterpolationDelayMs` (120 ms) trades smoothness for
  visible latency.
- `snapDistanceTiles` (3) and `maxFrameGapTicks` (3) bound corrections.
- `viewportDebounceMs` (200) and `viewportChangeThreshold` (1) bound server
  requests. The server also rate-limits `SetViewport`.
- Reduced motion sets interpolation delay to zero and disables camera easing.

## If a budget fails

1. Confirm the fixture path and payload size, not the network, is under test.
2. Check culling: `visible tiles` should be proportional to viewport area, not
   map area.
3. Check that network callbacks are not creating Pixi objects.
4. Profile the store: frame construction copies positions for all sims in the
   current replication set. If that dominates, switch to a sparse frame with
   carry-forward lookups.
5. Do not raise budgets without recording the device, resolution, simulation
   size and visible counts in this file.

# Step 04 — Fixed-step tick loop

**Depends:** 01
**Goal:** Replace the wall-clock timer with a robust fixed-step loop and a real delta time.

## Context

`System.Threading.Timer` at 40 ms with `DeltaTime = 1f` produces ~25 sim-seconds/second and
can queue callbacks when a tick is slow.

## Tasks

- [ ] Implement `SimulationLoop : BackgroundService` using `PeriodicTimer`.
- [ ] Use a fixed timestep (e.g., 1/20 s) and pass that constant delta to systems.
- [ ] Add an accumulator with a max catch-up count; log and drop excess.
- [ ] Add a tick counter and current tick-rate metric.
- [ ] Move system orchestration out of `SimulationEngine.Tick` into the loop.
- [ ] Remove the `System.Threading.Timer` and `const DeltaTime`.
- [ ] Make `Stop` drain/cancel cleanly.

## Files likely touched

- `Genelife.Api/Services/SimulationEngine.cs`
- new `Genelife.Api/Services/SimulationLoop.cs`
- `Genelife.Api/Services/SimulationManager.cs`, `Program.cs`

## Acceptance

- Stable tick cadence; no overlapping ticks.
- Slow tick does not produce unbounded backlog.
- Systems receive consistent `deltaTime`.

## Testing

- Inject an artificially slow tick; assert capped catch-up and no thread growth.

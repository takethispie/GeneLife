# E02 — Time and pay cycles

**Phase:** 1 — Foundation
**Depends:** E01
**Estimate:** 1 session
**Status:** Implemented (V1)

## Goal

Give the simulation one authoritative calendar so payroll, rent, pensions, taxes and aging agree on when a month, quarter and year end.

## Context

`SimulationLoop` runs a 20 Hz fixed step (`Genelife.API/Services/SimulationLoop.cs:9`). `PayrollSystem` already uses a 300-tick cadence (`PayrollSystem.cs:17`). Nothing else defines a month or year, and `Alive.Age` never changes.

## Design

`Genelife/SimulationClockOptions.cs`:

```csharp
public sealed class SimulationClockOptions
{
    public int TicksPerMonth { get; set; } = 100;
    public int TicksPerPayCycle { get; set; } = 300;
    public int TicksPerYear { get; set; } = 1200;
    public int StartingYear { get; set; } = 0;
}
```

`Genelife/SimulationClock.cs`:

```csharp
public sealed class SimulationClock
{
    public int TicksPerMonth { get; }
    public int TicksPerPayCycle { get; }
    public int TicksPerYear { get; }

    public bool IsMonthBoundary(long tick) => tick > 0 && tick % TicksPerMonth == 0;
    public bool IsPayCycleBoundary(long tick) => tick > 0 && tick % TicksPerPayCycle == 0;
    public bool IsYearBoundary(long tick) => tick > 0 && tick % TicksPerYear == 0;
    public int MonthOfYear(long tick) => (int)((tick / TicksPerMonth) % 12);
    public int YearOf(long tick) => StartingYear + (int)(tick / TicksPerYear);
    public long NextMonthBoundary(long tick) => ...
}
```

`SimulationContext` gains an optional clock:

```csharp
public readonly record struct SimulationContext(long Tick, float DeltaTime, SimulationClock? Clock = null)
{
    public SimulationClock Time => Clock ?? SimulationClock.Default;
}
```

Existing calls `new SimulationContext(tick, dt)` keep compiling and use the default cadence.

Months are 100 ticks, pay cycles are quarters of 300 ticks, years are 1,200 ticks. `TicksPerPayCycle` must divide `TicksPerYear`; validate at startup and fail fast.

## Tasks

- [ ] Add options and clock; register with `builder.Services.Configure<SimulationClockOptions>`.
- [ ] Inject the clock into `SimulationEngine` and pass it through `SimulationContext`.
- [ ] Replace magic `300`/`600` intervals in new systems with clock-derived cadences.
- [ ] Keep `PayrollSystem` at 300 ticks by default so existing tests stay valid.
- [ ] Add a configuration validation test: `TicksPerMonth <= TicksPerPayCycle <= TicksPerYear`, each divides the next.
- [ ] Expose `year`, `month` and `tickInYear` in `WorldDataStatus`.

## Files

- new `Genelife/SimulationClock.cs`, `Genelife/SimulationClockOptions.cs`
- `Genelife/Systems/ISimulationSystem.cs` (`SimulationContext`)
- `Genelife.API/Program.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/DTOs/WorldDataStatus.cs`

## Acceptance

- All new systems obtain month/pay-cycle/year boundaries from `SimulationClock`.
- Default configuration reproduces the existing 300-tick payroll cadence.
- Invalid configuration fails at startup with a clear message.
- Admin read model reports simulated year and month.

## Testing

- Boundary tests for ticks 0, 99, 100, 299, 300, 1199, 1200.
- Context compatibility test with the old two-argument constructor.

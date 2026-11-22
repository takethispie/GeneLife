namespace Genelife;

/// <summary>
/// Configures the simulation calendar. Payroll already runs every 300 ticks;
/// that cycle is the authoritative in-game quarter.
/// </summary>
public sealed class SimulationClockOptions
{
    public int TicksPerMonth { get; set; } = 100;
    public int TicksPerPayCycle { get; set; } = 300;
    public int TicksPerYear { get; set; } = 1200;
    public int StartingYear { get; set; } = 0;

    public void Validate()
    {
        if (TicksPerMonth <= 0 || TicksPerPayCycle <= 0 || TicksPerYear <= 0)
            throw new InvalidOperationException("Simulation clock intervals must be positive.");

        if (TicksPerMonth > TicksPerPayCycle || TicksPerPayCycle > TicksPerYear)
            throw new InvalidOperationException(
                "Simulation clock intervals must satisfy month <= pay cycle <= year.");

        if (TicksPerPayCycle % TicksPerMonth != 0)
            throw new InvalidOperationException("TicksPerPayCycle must be a multiple of TicksPerMonth.");

        if (TicksPerYear % TicksPerPayCycle != 0)
            throw new InvalidOperationException("TicksPerYear must be a multiple of TicksPerPayCycle.");
    }
}

/// <summary>
/// Single source of truth for month, pay-cycle and year boundaries. Systems use
/// this instead of hard-coded 100/300/1200 constants.
/// </summary>
public sealed class SimulationClock
{
    public static readonly SimulationClock Default = new(new SimulationClockOptions());

    public SimulationClock(SimulationClockOptions options)
    {
        options.Validate();
        TicksPerMonth = options.TicksPerMonth;
        TicksPerPayCycle = options.TicksPerPayCycle;
        TicksPerYear = options.TicksPerYear;
        StartingYear = options.StartingYear;
    }

    public int TicksPerMonth { get; }
    public int TicksPerPayCycle { get; }
    public int TicksPerYear { get; }
    public int StartingYear { get; }

    public bool IsMonthBoundary(long tick) => tick > 0 && tick % TicksPerMonth == 0;
    public bool IsPayCycleBoundary(long tick) => tick > 0 && tick % TicksPerPayCycle == 0;
    public bool IsYearBoundary(long tick) => tick > 0 && tick % TicksPerYear == 0;

    public int MonthOfYear(long tick) => (int)((tick / TicksPerMonth) % 12);
    public int YearOf(long tick) => StartingYear + (int)(tick / TicksPerYear);
    public long TickInYear(long tick) => tick % TicksPerYear;

    public long NextMonthBoundary(long tick) =>
        ((tick / TicksPerMonth) + 1) * TicksPerMonth;

    public long NextPayCycleBoundary(long tick) =>
        ((tick / TicksPerPayCycle) + 1) * TicksPerPayCycle;

    public long NextYearBoundary(long tick) =>
        ((tick / TicksPerYear) + 1) * TicksPerYear;
}

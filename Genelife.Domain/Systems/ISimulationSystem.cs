namespace Genelife.Systems;

/// <summary>
/// Per-tick state handed to every simulation system. Tick is the authoritative
/// tick number used for cadence scheduling and the optional clock resolves
/// month/pay-cycle/year boundaries.
/// </summary>
public readonly record struct SimulationContext(long Tick, float DeltaTime, SimulationClock? Clock = null)
{
    public SimulationClock Time => Clock ?? SimulationClock.Default;
}

/// <summary>
/// Tick interval for a system. A cadence of 1 (or less) runs every tick.
/// <para>
/// <see cref="Offset"/> shifts the phase of a periodic system so monthly and
/// pay-cycle work does not all land on the same tick. It is measured in ticks
/// after the base boundary: offset 2 with interval 100 runs on ticks 102, 202,
/// ... The first run still waits for one full interval, so a staggered system
/// never fires during the first month.
/// </para>
/// </summary>
public readonly record struct Cadence(int Interval, int Offset = 0)
{
    public static Cadence EveryTick => new(1);

    public bool ShouldRun(long tick)
    {
        if (Interval <= 1)
            return true;

        if (tick < Interval + Offset)
            return false;

        return (tick - Offset) % Interval == 0;
    }
}

/// <summary>
/// A simulation system with an explicit order and cadence. Systems are ordered
/// by Order only; no reflection or DI auto-ordering is used.
/// </summary>
public interface ISimulationSystem
{
    string Name { get; }

    int Order { get; }

    Cadence Cadence { get; }

    void Update(in SimulationContext context);
}

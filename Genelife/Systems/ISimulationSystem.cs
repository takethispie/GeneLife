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
/// </summary>
public readonly record struct Cadence(int Interval)
{
    public static Cadence EveryTick => new(1);

    public bool ShouldRun(long tick) => Interval <= 1 || tick % Interval == 0;
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

namespace Genelife.Systems;

/// <summary>
/// Per-tick state handed to every simulation system. <see cref="Tick"/> is the
/// authoritative tick number used for cadence scheduling; <see cref="DeltaTime"/>
/// is the fixed simulation step in seconds.
/// </summary>
public readonly record struct SimulationContext(long Tick, float DeltaTime);

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
/// by <see cref="Order"/> only; no reflection or DI auto-ordering is used.
/// </summary>
public interface ISimulationSystem
{
    string Name { get; }

    int Order { get; }

    Cadence Cadence { get; }

    void Update(in SimulationContext context);
}

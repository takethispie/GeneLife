using System.Diagnostics;

namespace Genelife.Application.Systems;

/// <summary>
/// Explicit, ordered registry of simulation systems. Registration is the single
/// place a system is wired in; duplicate orders or names are rejected so the
/// execution order stays deterministic.
/// </summary>
public sealed class SystemRegistry
{
    private readonly List<ISimulationSystem> systems = new();
    private readonly int samplingInterval;

    public SystemRegistry(int samplingInterval = 1)
    {
        if (samplingInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(samplingInterval));
        this.samplingInterval = samplingInterval;
    }

    public IReadOnlyList<ISimulationSystem> Systems => systems;

    public void Register(ISimulationSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        if (systems.Any(existing => existing.Order == system.Order))
        {
            throw new InvalidOperationException(
                $"System order {system.Order} is already registered ({system.Name}).");
        }

        if (systems.Any(existing => string.Equals(existing.Name, system.Name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"System '{system.Name}' is already registered.");
        }

        var index = systems.FindIndex(existing => existing.Order > system.Order);
        if (index < 0)
            systems.Add(system);
        else
            systems.Insert(index, system);
    }

    /// <summary>
    /// Runs every system whose cadence matches the current tick and returns the
    /// number of systems that ran. Timing and allocation are measured here so
    /// production and tests share one scheduling implementation.
    /// </summary>
    public int Update(in SimulationContext context, Action<SystemRunResult>? onSystemRun = null)
    {
        var ran = 0;
        var sampleTick = samplingInterval <= 1 || context.Tick % samplingInterval == 0;

        foreach (var system in systems)
        {
            var cadence = system.Cadence;
            if (!cadence.ShouldRun(context.Tick))
                continue;

            // Periodic systems run rarely, so always time them; sampling only
            // trims the instrumentation cost of the every-tick passes.
            var measure = onSystemRun is not null && (sampleTick || cadence.Interval > 1);

            // A staggered system runs later than its base boundary, but it sees
            // the logical boundary tick. That keeps tick-derived work (RNG
            // streams, due dates, event timestamps) identical to the
            // unstaggered schedule while spreading the actual work.
            var systemContext = cadence.Offset == 0
                ? context
                : new SimulationContext(context.Tick - cadence.Offset, context.DeltaTime, context.Clock);

            if (!measure)
            {
                system.Update(in systemContext);
                ran++;
                continue;
            }

            var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            system.Update(in systemContext);
            ran++;

            if (onSystemRun is not null)
            {
                var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var allocated = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedStart);
                onSystemRun(new SystemRunResult(system.Name, elapsed, allocated));
            }
        }

        return ran;
    }
}

public readonly record struct SystemRunResult(string Name, double Milliseconds, long AllocatedBytes);


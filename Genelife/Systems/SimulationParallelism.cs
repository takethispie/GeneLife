using Arch.Core;
using Schedulers;

namespace Genelife.Systems;

/// <summary>
/// Lazily installs the process-wide Arch job scheduler used by the parallel
/// per-entity passes. Arch requires <see cref="World.SharedJobScheduler"/> to be
/// assigned before any parallel query; the scheduler lives for the process.
/// </summary>
internal static class SimulationParallelism
{
    private static readonly Lazy<JobScheduler> Scheduler = new(() =>
    {
        var scheduler = new JobScheduler(new JobScheduler.Config
        {
            ThreadCount = Math.Max(1, Environment.ProcessorCount - 1),
            ThreadPrefixName = "genelife-parallel"
        });
        World.SharedJobScheduler = scheduler;
        return scheduler;
    });

    public static void EnsureScheduler() => _ = Scheduler.Value;
}

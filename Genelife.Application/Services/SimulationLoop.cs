using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Genelife.Api.Services;

public class SimulationLoop : BackgroundService
{
    private readonly SimulationEngine engine;
    private readonly ILogger<SimulationLoop>? logger;
    private const float FixedDeltaTime = 1f / 20f;

    /// <summary>Fixed simulation ticks per real second — the unit travel durations are quoted in.</summary>
    public const int TicksPerSecond = 20;
    private const int MaxCatchUpTicks = 5;
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(FixedDeltaTime);
    private static readonly double TickBudgetMs = FixedStep.TotalMilliseconds;
    private static readonly TimeSpan DropLogInterval = TimeSpan.FromSeconds(5);

    private readonly FixedStepScheduler scheduler = new(FixedStep, MaxCatchUpTicks);
    private readonly long startTimestamp = Stopwatch.GetTimestamp();

    private long tickCount;
    private long overrunCount;
    private long droppedTicks;
    private long lastDropLogTimestamp;
    private long lastOverrunLogTimestamp;
    private double lastTickMilliseconds;
    private readonly PeriodicTimer timer = new(FixedStep);

    public SimulationLoop(SimulationEngine engine, ILogger<SimulationLoop>? logger = null)
    {
        this.engine = engine;
        this.logger = logger;

        // The hosted loop is the production path: run snapshot construction on
        // its own consumer thread so it does not count against the tick budget.
        engine.AsyncSnapshots = true;
    }

    public long TickCount => Volatile.Read(ref tickCount);
    public long OverrunCount => Volatile.Read(ref overrunCount);
    public long DroppedTicks => Volatile.Read(ref droppedTicks);
    public double LastTickMilliseconds => Volatile.Read(ref lastTickMilliseconds);
    public double TickBudgetMilliseconds => TickBudgetMs;

    public double EffectiveTickRatePerSecond()
    {
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        return elapsed > 0 ? Volatile.Read(ref tickCount) / elapsed : 0;
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // A dedicated long-running thread keeps the fixed-step loop off the
        // shared thread pool, so request load cannot delay a tick.
        return Task.Factory.StartNew(
            () => RunLoop(cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void RunLoop(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var lastTime = sw.Elapsed;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                timer.WaitForNextTickAsync(cancellationToken).AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var currentTime = sw.Elapsed;
            var frameDelta = currentTime - lastTime;
            lastTime = currentTime;

            var steps = scheduler.Advance(frameDelta, out var discarded);

            for (var i = 0; i < steps; i++)
            {
                engine.Tick(FixedDeltaTime);
                Interlocked.Increment(ref tickCount);

                var tickMilliseconds = engine.LastTickMilliseconds;
                Volatile.Write(ref lastTickMilliseconds, tickMilliseconds);

                if (tickMilliseconds > TickBudgetMs)
                {
                    Interlocked.Increment(ref overrunCount);
                    if (logger is not null)
                        TryLogOverrun(tickMilliseconds);
                }
            }

            if (discarded > TimeSpan.Zero)
            {
                var dropped = Math.Max(1, (long)Math.Round(discarded / FixedStep));
                var totalDropped = Interlocked.Add(ref droppedTicks, dropped);
                var now = Stopwatch.GetTimestamp();
                var lastLog = Interlocked.Read(ref lastDropLogTimestamp);

                if (logger is not null &&
                    (lastLog == 0 || Stopwatch.GetElapsedTime(lastLog, now) >= DropLogInterval))
                {
                    Interlocked.Exchange(ref lastDropLogTimestamp, now);
                    logger.LogWarning(
                        "Simulation loop dropped {DroppedTicks} catch-up tick(s); backlog exceeded {MaxCatchUp} ticks (total dropped: {TotalDropped})",
                        dropped,
                        MaxCatchUpTicks,
                        totalDropped);
                }
            }
        }
    }

    /// <summary>
    /// Logs an overrun at most once per <see cref="DropLogInterval"/>. Building the
    /// metrics snapshot takes the metrics lock and sorts every system, which is
    /// exactly the work that makes a sustained overrun worse, so the expensive
    /// part is throttled while <see cref="OverrunCount"/> keeps counting.
    /// </summary>
    private void TryLogOverrun(double tickMilliseconds)
    {
        var now = Stopwatch.GetTimestamp();
        var lastLog = Interlocked.Read(ref lastOverrunLogTimestamp);
        if (lastLog != 0 && Stopwatch.GetElapsedTime(lastLog, now) < DropLogInterval)
            return;

        Interlocked.Exchange(ref lastOverrunLogTimestamp, now);
        LogOverrun(tickMilliseconds);
    }

    private void LogOverrun(double tickMilliseconds)
    {
        var metrics = engine.GetMetricsSnapshot();
        var top = metrics.Systems
            .OrderByDescending(system => system.AverageMilliseconds)
            .Take(3)
            .Select(system => $"{system.Name}={system.AverageMilliseconds:F2}ms/{system.AllocatedBytes}B");

        logger!.LogWarning(
            "Simulation tick overran budget: {TickMs:F2}ms > {BudgetMs:F2}ms; overruns={Overruns}; top=[{Top}]",
            tickMilliseconds,
            TickBudgetMs,
            OverrunCount,
            string.Join(", ", top));
    }

    public double CurrentTickRatePerSecond()
    {
        return 1.0 / FixedDeltaTime;
    }
}

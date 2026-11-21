using System.Diagnostics;
using System.Globalization;

namespace Genelife.Api.Services;

/// <summary>
/// Lightweight, dependency-free performance counters for the simulation tick loop.
/// All recording is allocation-free on the hot path; formatting only happens when a
/// window is logged or a snapshot is requested.
/// </summary>
public sealed class Metrics
{
    private const string LogPrefix = "[metrics]";

    private readonly object sync = new();
    private readonly int histogramCapacity;
    private readonly int logEveryTicks;
    private readonly Action<string> logSink;
    private readonly Func<long> timestampProvider;
    private readonly double[] tickDurationsMs;
    private readonly Dictionary<string, SystemStats> systems = new(StringComparer.Ordinal);

    private int tickDurationCount;
    private int tickDurationNext;

    private long tickCount;
    private double totalTickMs;
    private double lastTickMs;

    private long totalAllocatedBytes;
    private long lastAllocatedBytes;

    private long windowTickCount;
    private long windowAllocatedBytes;
    private long windowStartTimestamp;
    private bool windowStarted;
    private int windowGen0Start;
    private int windowGen1Start;
    private int windowGen2Start;
    private int windowEntitiesCreated;
    private int windowEntitiesDestroyed;
    private int entityCount = -1;

    public Metrics(
        int histogramCapacity = 1024,
        int logEveryTicks = 250,
        Action<string>? logSink = null,
        Func<long>? timestampProvider = null)
    {
        if (histogramCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(histogramCapacity));
        if (logEveryTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(logEveryTicks));

        this.histogramCapacity = histogramCapacity;
        this.logEveryTicks = logEveryTicks;
        this.logSink = logSink ?? Console.WriteLine;
        this.timestampProvider = timestampProvider ?? Stopwatch.GetTimestamp;
        tickDurationsMs = new double[histogramCapacity];
    }

    /// <summary>Records the elapsed time of a single system update.</summary>
    public void RecordSystem(string name, double elapsedMilliseconds)
    {
        lock (sync)
        {
            if (!systems.TryGetValue(name, out var stats))
            {
                stats = new SystemStats(name);
                systems[name] = stats;
            }

            stats.Calls++;
            stats.TotalMilliseconds += elapsedMilliseconds;
            if (elapsedMilliseconds > stats.MaxMilliseconds)
                stats.MaxMilliseconds = elapsedMilliseconds;
        }
    }

    /// <summary>
    /// Records one completed tick. <paramref name="allocatedBytes"/> should be the
    /// managed allocation delta observed on the ticking thread.
    /// </summary>
    public void RecordTick(double durationMilliseconds, long allocatedBytes, int entities)
    {
        string? lineToLog = null;

        lock (sync)
        {
            if (durationMilliseconds < 0)
                durationMilliseconds = 0;
            if (allocatedBytes < 0)
                allocatedBytes = 0;

            tickCount++;
            totalTickMs += durationMilliseconds;
            lastTickMs = durationMilliseconds;
            AddTickDuration(durationMilliseconds);

            totalAllocatedBytes += allocatedBytes;
            lastAllocatedBytes = allocatedBytes;

            var now = timestampProvider();
            if (!windowStarted)
            {
                windowStarted = true;
                windowStartTimestamp = now;
                CaptureGcBaseline();
            }

            windowTickCount++;
            windowAllocatedBytes += allocatedBytes;

            if (entityCount >= 0)
            {
                var delta = entities - entityCount;
                if (delta > 0)
                    windowEntitiesCreated += delta;
                else if (delta < 0)
                    windowEntitiesDestroyed -= delta;
            }

            entityCount = entities;

            if (windowTickCount >= logEveryTicks)
            {
                lineToLog = FormatLine(SnapshotLocked());
                ResetWindow(now);
            }
        }

        if (lineToLog != null)
            logSink(lineToLog);
    }

    /// <summary>Emits the current window immediately, if anything has been recorded.</summary>
    public void Flush()
    {
        string? lineToLog = null;

        lock (sync)
        {
            if (tickCount > 0)
            {
                lineToLog = FormatLine(SnapshotLocked());
                ResetWindow(timestampProvider());
            }
        }

        if (lineToLog != null)
            logSink(lineToLog);
    }

    public MetricsSnapshot Snapshot()
    {
        lock (sync)
        {
            return SnapshotLocked();
        }
    }

    private void AddTickDuration(double durationMilliseconds)
    {
        tickDurationsMs[tickDurationNext] = durationMilliseconds;
        tickDurationNext = (tickDurationNext + 1) % histogramCapacity;
        if (tickDurationCount < histogramCapacity)
            tickDurationCount++;
    }

    private MetricsSnapshot SnapshotLocked()
    {
        var now = windowStarted ? timestampProvider() : 0;
        var elapsedSeconds = windowStarted
            ? Math.Max(0, now - windowStartTimestamp) / (double)Stopwatch.Frequency
            : 0;

        var gen0 = windowStarted ? GC.CollectionCount(0) - windowGen0Start : 0;
        var gen1 = windowStarted ? GC.CollectionCount(1) - windowGen1Start : 0;
        var gen2 = windowStarted ? GC.CollectionCount(2) - windowGen2Start : 0;

        var tickRate = elapsedSeconds > 0 ? windowTickCount / elapsedSeconds : 0;
        var createdPerMinute = elapsedSeconds > 0 ? windowEntitiesCreated / elapsedSeconds * 60 : 0;
        var destroyedPerMinute = elapsedSeconds > 0 ? windowEntitiesDestroyed / elapsedSeconds * 60 : 0;

        var systemMetrics = new SystemMetric[systems.Count];
        var index = 0;
        foreach (var stats in systems.Values)
            systemMetrics[index++] = stats.ToMetric();

        return new MetricsSnapshot(
            TickCount: tickCount,
            WindowTickCount: windowTickCount,
            WindowSeconds: elapsedSeconds,
            TickRatePerSecond: tickRate,
            LastTickMilliseconds: lastTickMs,
            AverageTickMilliseconds: tickCount > 0 ? totalTickMs / tickCount : 0,
            P50TickMilliseconds: PercentileLocked(0.50),
            P95TickMilliseconds: PercentileLocked(0.95),
            P99TickMilliseconds: PercentileLocked(0.99),
            TotalAllocatedBytes: totalAllocatedBytes,
            WindowAllocatedBytes: windowAllocatedBytes,
            LastTickAllocatedBytes: lastAllocatedBytes,
            AllocatedBytesPerTick: windowTickCount > 0 ? windowAllocatedBytes / (double)windowTickCount : 0,
            Gen0Collections: gen0,
            Gen1Collections: gen1,
            Gen2Collections: gen2,
            Gen0CollectionsPerMinute: elapsedSeconds > 0 ? gen0 / elapsedSeconds * 60 : 0,
            Gen1CollectionsPerMinute: elapsedSeconds > 0 ? gen1 / elapsedSeconds * 60 : 0,
            Gen2CollectionsPerMinute: elapsedSeconds > 0 ? gen2 / elapsedSeconds * 60 : 0,
            EntityCount: entityCount < 0 ? 0 : entityCount,
            EntitiesCreated: windowEntitiesCreated,
            EntitiesDestroyed: windowEntitiesDestroyed,
            EntitiesCreatedPerMinute: createdPerMinute,
            EntitiesDestroyedPerMinute: destroyedPerMinute,
            Systems: systemMetrics);
    }

    private double PercentileLocked(double percentile)
    {
        if (tickDurationCount == 0)
            return 0;

        var sorted = new double[tickDurationCount];
        Array.Copy(tickDurationsMs, sorted, tickDurationCount);
        Array.Sort(sorted);

        var rank = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Length - 1)];
    }

    private void CaptureGcBaseline()
    {
        windowGen0Start = GC.CollectionCount(0);
        windowGen1Start = GC.CollectionCount(1);
        windowGen2Start = GC.CollectionCount(2);
    }

    private void ResetWindow(long now)
    {
        windowTickCount = 0;
        windowAllocatedBytes = 0;
        windowEntitiesCreated = 0;
        windowEntitiesDestroyed = 0;
        windowStartTimestamp = now;
        CaptureGcBaseline();
    }

    private static string FormatLine(MetricsSnapshot snapshot)
    {
        var top = snapshot.Systems
            .OrderByDescending(system => system.AverageMilliseconds)
            .Take(3)
            .Select(system => string.Create(
                CultureInfo.InvariantCulture,
                $"{system.Name}={system.AverageMilliseconds:F2}ms"));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{LogPrefix} ticks={snapshot.TickCount} window={snapshot.WindowTickCount} " +
            $"rate={snapshot.TickRatePerSecond:F1}/s " +
            $"tick_ms p50={snapshot.P50TickMilliseconds:F2} p95={snapshot.P95TickMilliseconds:F2} " +
            $"p99={snapshot.P99TickMilliseconds:F2} avg={snapshot.AverageTickMilliseconds:F2} " +
            $"alloc_per_tick={snapshot.AllocatedBytesPerTick:F0}B " +
            $"gc0={snapshot.Gen0Collections} gc1={snapshot.Gen1Collections} gc2={snapshot.Gen2Collections} " +
            $"gc_per_min={snapshot.Gen0CollectionsPerMinute:F0}/{snapshot.Gen1CollectionsPerMinute:F0}/{snapshot.Gen2CollectionsPerMinute:F0} " +
            $"entities={snapshot.EntityCount} " +
            $"churn_per_min=+{snapshot.EntitiesCreatedPerMinute:F0}/-{snapshot.EntitiesDestroyedPerMinute:F0} " +
            $"top=[{string.Join(", ", top)}]");
    }

    private sealed class SystemStats
    {
        public SystemStats(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public long Calls { get; set; }
        public double TotalMilliseconds { get; set; }
        public double MaxMilliseconds { get; set; }

        public SystemMetric ToMetric() => new(
            Name,
            Calls,
            TotalMilliseconds,
            Calls > 0 ? TotalMilliseconds / Calls : 0,
            MaxMilliseconds);
    }
}

public sealed record SystemMetric(
    string Name,
    long Calls,
    double TotalMilliseconds,
    double AverageMilliseconds,
    double MaxMilliseconds);

public sealed record MetricsSnapshot(
    long TickCount,
    long WindowTickCount,
    double WindowSeconds,
    double TickRatePerSecond,
    double LastTickMilliseconds,
    double AverageTickMilliseconds,
    double P50TickMilliseconds,
    double P95TickMilliseconds,
    double P99TickMilliseconds,
    long TotalAllocatedBytes,
    long WindowAllocatedBytes,
    long LastTickAllocatedBytes,
    double AllocatedBytesPerTick,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    double Gen0CollectionsPerMinute,
    double Gen1CollectionsPerMinute,
    double Gen2CollectionsPerMinute,
    int EntityCount,
    int EntitiesCreated,
    int EntitiesDestroyed,
    double EntitiesCreatedPerMinute,
    double EntitiesDestroyedPerMinute,
    IReadOnlyList<SystemMetric> Systems);

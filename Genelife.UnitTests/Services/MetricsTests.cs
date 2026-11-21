using System.Diagnostics;
using Genelife.Api.Services;

namespace Genelife.UnitTests.Services;

public class MetricsTests
{
    [Fact]
    public void Snapshot_ComputesTickPercentilesAndAverage()
    {
        var clock = new FakeClock();
        var metrics = CreateMetrics(clock);

        for (var i = 1; i <= 100; i++)
            metrics.RecordTick(i, 0, 0);

        var snapshot = metrics.Snapshot();

        Assert.Equal(100, snapshot.TickCount);
        Assert.Equal(50, snapshot.P50TickMilliseconds, 6);
        Assert.Equal(95, snapshot.P95TickMilliseconds, 6);
        Assert.Equal(99, snapshot.P99TickMilliseconds, 6);
        Assert.Equal(50.5, snapshot.AverageTickMilliseconds, 6);
    }

    [Fact]
    public void Snapshot_ComputesEffectiveTickRate()
    {
        var clock = new FakeClock();
        var metrics = CreateMetrics(clock);

        for (var i = 0; i < 10; i++)
        {
            metrics.RecordTick(1, 0, 0);
            clock.AdvanceSeconds(0.04);
        }

        var snapshot = metrics.Snapshot();

        Assert.Equal(10, snapshot.WindowTickCount);
        Assert.Equal(25, snapshot.TickRatePerSecond, 3);
    }

    [Fact]
    public void Snapshot_TracksAllocationsPerTick()
    {
        var clock = new FakeClock();
        var metrics = CreateMetrics(clock);

        metrics.RecordTick(1, 100, 0);
        metrics.RecordTick(1, 200, 0);
        metrics.RecordTick(1, 300, 0);

        var snapshot = metrics.Snapshot();

        Assert.Equal(600, snapshot.TotalAllocatedBytes);
        Assert.Equal(600, snapshot.WindowAllocatedBytes);
        Assert.Equal(300, snapshot.LastTickAllocatedBytes);
        Assert.Equal(200, snapshot.AllocatedBytesPerTick, 6);
    }

    [Fact]
    public void Snapshot_TracksEntityCountAndObservedChurn()
    {
        var clock = new FakeClock();
        var metrics = CreateMetrics(clock);

        metrics.RecordTick(1, 0, 100);
        metrics.RecordTick(1, 0, 110);
        metrics.RecordTick(1, 0, 105);

        var snapshot = metrics.Snapshot();

        Assert.Equal(105, snapshot.EntityCount);
        Assert.Equal(10, snapshot.EntitiesCreated);
        Assert.Equal(5, snapshot.EntitiesDestroyed);
    }

    [Fact]
    public void Snapshot_AggregatesPerSystemTimings()
    {
        var clock = new FakeClock();
        var metrics = CreateMetrics(clock);

        metrics.RecordSystem("Alpha", 1.5);
        metrics.RecordSystem("Alpha", 4.5);
        metrics.RecordSystem("Beta", 2);

        var snapshot = metrics.Snapshot();
        var alpha = Assert.Single(snapshot.Systems, system => system.Name == "Alpha");
        var beta = Assert.Single(snapshot.Systems, system => system.Name == "Beta");

        Assert.Equal(2, alpha.Calls);
        Assert.Equal(6, alpha.TotalMilliseconds, 6);
        Assert.Equal(3, alpha.AverageMilliseconds, 6);
        Assert.Equal(4.5, alpha.MaxMilliseconds, 6);

        Assert.Equal(1, beta.Calls);
        Assert.Equal(2, beta.TotalMilliseconds, 6);
    }

    [Fact]
    public void Snapshot_TracksGen0Collections()
    {
        var clock = new FakeClock();
        var metrics = CreateMetrics(clock);

        metrics.RecordTick(1, 0, 0);
        GC.Collect(0);
        metrics.RecordTick(1, 0, 0);
        clock.AdvanceSeconds(1);

        var snapshot = metrics.Snapshot();

        Assert.True(snapshot.Gen0Collections >= 1);
        Assert.True(snapshot.Gen0CollectionsPerMinute >= 1);
    }

    [Fact]
    public void RecordTick_LogsAtConfiguredInterval()
    {
        var clock = new FakeClock();
        var lines = new List<string>();
        var metrics = new Metrics(
            histogramCapacity: 16,
            logEveryTicks: 2,
            logSink: lines.Add,
            timestampProvider: clock.GetTimestamp);

        metrics.RecordTick(1, 0, 0);
        Assert.Empty(lines);

        metrics.RecordTick(2, 0, 0);

        var line = Assert.Single(lines);
        Assert.Contains("[metrics]", line);
        Assert.Contains("p50=", line);
        Assert.Contains("p95=", line);
        Assert.Contains("p99=", line);
        Assert.Contains("rate=", line);
        Assert.Contains("gc0=", line);
        Assert.Contains("entities=", line);
    }

    private static Metrics CreateMetrics(FakeClock clock)
    {
        return new Metrics(
            histogramCapacity: 1024,
            logEveryTicks: 1000,
            logSink: _ => { },
            timestampProvider: clock.GetTimestamp);
    }

    private sealed class FakeClock
    {
        private long now = 1_000_000_000;

        public long GetTimestamp() => now;

        public void AdvanceSeconds(double seconds)
        {
            now += (long)(seconds * Stopwatch.Frequency);
        }
    }
}

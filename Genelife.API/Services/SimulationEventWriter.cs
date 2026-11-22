using System.Threading.Channels;
using Marten;

namespace Genelife.Api.Services;

/// <summary>
/// Persists simulation events off the tick thread. The tick loop enqueues events and
/// never waits for the event store, so database latency cannot stall the fixed-step loop.
/// </summary>
public sealed class SimulationEventWriter : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DropReportInterval = TimeSpan.FromSeconds(5);
    private const int MaxBatchSize = 2048;

    private readonly SimulationEngine engine;
    private readonly IDocumentStore? documentStore;
    private readonly ILogger<SimulationEventWriter>? logger;

    public SimulationEventWriter(
        SimulationEngine engine,
        IDocumentStore? documentStore = null,
        ILogger<SimulationEventWriter>? logger = null)
    {
        this.engine = engine;
        this.documentStore = documentStore;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (documentStore is null)
            return;

        var reader = engine.OutboundEvents;
        var batch = new List<object>(MaxBatchSize);
        var lastDropped = 0L;
        var lastDropReport = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await reader.WaitToReadAsync(stoppingToken))
                    break;

                await Task.Delay(FlushInterval, stoppingToken);

                while (batch.Count < MaxBatchSize && reader.TryRead(out var e))
                    batch.Add(e);

                if (batch.Count > 0)
                {
                    await FlushAsync(batch, stoppingToken);
                    batch.Clear();
                }

                var now = DateTime.UtcNow;
                if (now - lastDropReport >= DropReportInterval)
                {
                    var dropped = engine.DroppedEventCount;
                    if (dropped > lastDropped)
                    {
                        logger?.LogWarning(
                            "Dropped {DroppedEvents} simulation event(s); persistence backlog exceeded capacity",
                            dropped - lastDropped);
                        lastDropped = dropped;
                    }

                    lastDropReport = now;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to persist {Count} simulation event(s)", batch.Count);
                batch.Clear();
            }
        }

        await FlushRemainingAsync(reader, batch);
    }

    private async Task FlushRemainingAsync(ChannelReader<object> reader, List<object> batch)
    {
        while (reader.TryRead(out var e))
        {
            batch.Add(e);
            if (batch.Count < MaxBatchSize)
                continue;

            await TryFlushAsync(batch);
            batch.Clear();
        }

        if (batch.Count > 0)
            await TryFlushAsync(batch);
    }

    private async Task TryFlushAsync(List<object> batch)
    {
        try
        {
            await FlushAsync(batch, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to persist {Count} simulation event(s) during shutdown", batch.Count);
        }
    }

    private async Task FlushAsync(List<object> batch, CancellationToken cancellationToken)
    {
        await using var session = documentStore!.LightweightSession();

        foreach (var e in batch)
            session.Events.Append(Guid.NewGuid(), e);

        await session.SaveChangesAsync(cancellationToken);
    }
}

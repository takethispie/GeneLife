using System.Threading.Channels;
using Genelife.Messages.Events;
using Marten;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Genelife.Api.Services;

/// <summary>
/// Persists simulation events off the tick thread. The tick loop enqueues events and
/// never waits for the event store, so database latency cannot stall the fixed-step loop.
/// The same drain fans the events out to any registered <see cref="ISimulationEventSink"/>
/// (for example the Town bell digest), so a second consumer never has to own the
/// single-reader outbound channel.
/// </summary>
public sealed class SimulationEventWriter : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DropReportInterval = TimeSpan.FromSeconds(5);
    private const int MaxBatchSize = 2048;

    private readonly SimulationEngine engine;
    private readonly IDocumentStore? documentStore;
    private readonly ISimulationEventSink? sink;
    private readonly ILogger<SimulationEventWriter>? logger;

    public SimulationEventWriter(
        SimulationEngine engine,
        IDocumentStore? documentStore = null,
        ISimulationEventSink? sink = null,
        ILogger<SimulationEventWriter>? logger = null)
    {
        this.engine = engine;
        this.documentStore = documentStore;
        this.sink = sink;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                    await DispatchAsync(batch, stoppingToken);
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

            await TryDispatchAsync(batch);
            batch.Clear();
        }

        if (batch.Count > 0)
            await TryDispatchAsync(batch);
    }

    private async Task TryDispatchAsync(List<object> batch)
    {
        try
        {
            await DispatchAsync(batch, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to persist {Count} simulation event(s) during shutdown", batch.Count);
        }
    }

    /// <summary>
    /// Fans the batch out to registered sinks, then persists it. A sink failure
    /// is logged and swallowed — the digest is a read model and must never take
    /// down persistence.
    /// </summary>
    private async Task DispatchAsync(List<object> batch, CancellationToken cancellationToken)
    {
        ForwardToSinks(batch);

        if (documentStore is not null)
            await FlushAsync(batch, cancellationToken);
    }

    private void ForwardToSinks(List<object> batch)
    {
        if (sink is null)
            return;

        var tick = engine.CurrentTick;
        foreach (var e in batch)
        {
            try
            {
                sink.OnEvent(e, tick);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Event sink failed for {EventType}", e.GetType().Name);
            }
        }
    }

    private async Task FlushAsync(List<object> batch, CancellationToken cancellationToken)
    {
        // Create documents in a separate transaction so inline update
        // projections can see them. Without this, a GenosCreated followed by
        // GenosNeedsChanged in the same batch updates a document that is still
        // pending in the session and silently loses the update.
        var creates = new List<object>();
        var updates = new List<object>();
        foreach (var e in batch)
        {
            if (e is GenosCreated or CompanyCreated)
                creates.Add(e);
            else
                updates.Add(e);
        }

        if (creates.Count > 0)
            await AppendBatchAsync(creates, cancellationToken);
        if (updates.Count > 0)
            await AppendBatchAsync(updates, cancellationToken);
    }

    private async Task AppendBatchAsync(List<object> batch, CancellationToken cancellationToken)
    {
        await using var session = documentStore!.LightweightSession();

        foreach (var e in batch)
            session.Events.Append(Guid.NewGuid(), e);

        await session.SaveChangesAsync(cancellationToken);
    }
}

using Genelife.Api.Auth;
using Genelife.Api.Snapshots;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Api.Services;

/// <summary>
/// Pushes the latest world state to every connected client. Clients that have
/// acknowledged a tick still inside <see cref="SnapshotHistory"/> receive a
/// delta; everyone else receives a full snapshot.
/// </summary>
public sealed class SnapshotBroadcastService : BackgroundService
{
    private readonly SimulationEngine engine;
    private readonly IHubContext<Hubs.WorldHub> hubContext;
    private readonly IConnectionRegistry registry;
    private readonly ReplicationTracker replicationTracker;
    private readonly ReplicationMetrics replicationMetrics;
    private readonly ILogger<SnapshotBroadcastService>? logger;

    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromMilliseconds(40);
    private long lastBroadcastedTick;

    public SnapshotBroadcastService(
        SimulationEngine engine,
        IHubContext<Hubs.WorldHub> hubContext,
        IConnectionRegistry registry,
        ReplicationTracker replicationTracker,
        ReplicationMetrics replicationMetrics,
        ILogger<SnapshotBroadcastService>? logger = null)
    {
        this.engine = engine;
        this.hubContext = hubContext;
        this.registry = registry;
        this.replicationTracker = replicationTracker;
        this.replicationMetrics = replicationMetrics;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(BroadcastInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var snapshot = engine.GetLatestSnapshot();
                if (snapshot is null || snapshot.Tick <= Interlocked.Read(ref lastBroadcastedTick))
                    continue;

                Interlocked.Exchange(ref lastBroadcastedTick, snapshot.Tick);

                var groups = registry.GetConnectionIds()
                    .Select(connectionId => (
                        ConnectionId: connectionId,
                        AcknowledgedTick: replicationTracker.GetAcknowledgedTick(connectionId)))
                    .Where(entry => entry.AcknowledgedTick < snapshot.Tick)
                    .GroupBy(entry => entry.AcknowledgedTick);

                foreach (var group in groups)
                {
                    var delta = engine.SnapshotHistory.BuildDelta(group.Key);
                    if (delta is null)
                        continue;

                    if (delta.Full)
                    {
                        var full = new WorldSnapshot(delta.Tick, delta.Version, delta.Sims, delta.Companies);
                        var bytes = ReplicationMetrics.EstimateBytes(full);

                        foreach (var entry in group)
                        {
                            replicationMetrics.Record(entry.ConnectionId, full: true, bytes);
                            await hubContext.Clients.Client(entry.ConnectionId)
                                .SendAsync("SendSnapshot", full, cancellationToken);
                        }

                        continue;
                    }

                    if (delta.Sims.Count == 0
                        && delta.Companies.Count == 0
                        && delta.RemovedSims.Count == 0
                        && delta.RemovedCompanies.Count == 0)
                    {
                        continue;
                    }

                    var deltaBytes = ReplicationMetrics.EstimateBytes(delta);
                    foreach (var entry in group)
                    {
                        replicationMetrics.Record(entry.ConnectionId, full: false, deltaBytes);
                        await hubContext.Clients.Client(entry.ConnectionId)
                            .SendAsync("SendSnapshotDelta", delta, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Snapshot broadcast failed");
            }
        }
    }
}

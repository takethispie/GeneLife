using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Api.Services;

/// <summary>
/// Pushes the latest world state to every connected client. Clients that have
/// acknowledged a tick still inside <see cref="SnapshotHistory"/> receive a
/// delta; everyone else receives a full snapshot. Optional viewport
/// subscriptions filter the dynamic sim payload.
/// </summary>
public sealed class SnapshotBroadcastService : BackgroundService
{
    private readonly SimulationEngine engine;
    private readonly IHubContext<Hubs.WorldHub> hubContext;
    private readonly IConnectionRegistry registry;
    private readonly ReplicationTracker replicationTracker;
    private readonly ReplicationMetrics replicationMetrics;
    private readonly ViewportRegistry viewports;
    private readonly ILogger<SnapshotBroadcastService>? logger;

    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromMilliseconds(40);
    private long lastBroadcastedTick;
    private int lastMapVersion;
    private Dictionary<ulong, MapBuildingStatus> lastMapBuildings = new();

    public SnapshotBroadcastService(
        SimulationEngine engine,
        IHubContext<Hubs.WorldHub> hubContext,
        IConnectionRegistry registry,
        ReplicationTracker replicationTracker,
        ReplicationMetrics replicationMetrics,
        ViewportRegistry? viewports = null,
        ILogger<SnapshotBroadcastService>? logger = null)
    {
        this.engine = engine;
        this.hubContext = hubContext;
        this.registry = registry;
        this.replicationTracker = replicationTracker;
        this.replicationMetrics = replicationMetrics;
        this.viewports = viewports ?? new ViewportRegistry();
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
                if (snapshot is not null && snapshot.Tick > Interlocked.Read(ref lastBroadcastedTick))
                {
                    Interlocked.Exchange(ref lastBroadcastedTick, snapshot.Tick);
                    await BroadcastWorld(snapshot, cancellationToken);
                    await BroadcastMap(cancellationToken);
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

    private async Task BroadcastWorld(WorldSnapshot snapshot, CancellationToken cancellationToken)
    {
        foreach (var connectionId in registry.GetConnectionIds())
        {
            var acknowledged = replicationTracker.GetAcknowledgedTick(connectionId);
            if (acknowledged >= snapshot.Tick)
                continue;

            var delta = engine.SnapshotHistory.BuildDelta(acknowledged);
            if (delta is null)
                continue;

            var viewport = viewports.TryGet(connectionId, out var active) ? active : (Viewport?)null;
            registry.TryGetPlayerId(connectionId, out var playerId);
            var ownSim = playerId is null ? 0 : engine.GetPlayerSimNetworkId(playerId);

            if (delta.Full)
            {
                var full = new WorldSnapshot(delta.Tick, delta.Version, delta.Sims, delta.Companies);
                if (viewport is { } fullViewport)
                    full = AoiFilter.FilterSnapshotByViewport(full, fullViewport, ownSim);
                var bytes = ReplicationMetrics.EstimateBytes(full);
                replicationMetrics.Record(connectionId, full: true, bytes);
                await hubContext.Clients.Client(connectionId)
                    .SendAsync("SendSnapshot", full, cancellationToken);
                continue;
            }

            if (viewport is { } filteredViewport)
                delta = AoiFilter.FilterDeltaByViewport(delta, filteredViewport, ownSim);

            if (delta.Sims.Count == 0
                && delta.Companies.Count == 0
                && delta.RemovedSims.Count == 0
                && delta.RemovedCompanies.Count == 0)
                continue;

            var deltaBytes = ReplicationMetrics.EstimateBytes(delta);
            replicationMetrics.Record(connectionId, full: false, deltaBytes);
            await hubContext.Clients.Client(connectionId)
                .SendAsync("SendSnapshotDelta", delta, cancellationToken);
        }
    }

    private async Task BroadcastMap(CancellationToken cancellationToken)
    {
        if (engine.MapVersion == lastMapVersion)
            return;

        var map = engine.BuildMapStatus();
        if (map.Version == lastMapVersion)
            return;

        if (lastMapVersion > 0)
        {
            var current = map.BuildingList.ToDictionary(building => building.NetworkId);
            var delta = new MapDelta
            {
                MapId = map.MapId,
                FromVersion = lastMapVersion,
                ToVersion = map.Version,
                Tick = engine.CurrentTick
            };

            foreach (var building in current.Values)
            {
                if (!lastMapBuildings.TryGetValue(building.NetworkId, out var previous))
                    delta.Added.Add(building);
                else if (!BuildingsEqual(previous, building))
                    delta.Updated.Add(building);
            }

            foreach (var id in lastMapBuildings.Keys)
            {
                if (!current.ContainsKey(id))
                    delta.Removed.Add(id);
            }

            if (delta.Added.Count > 0 || delta.Updated.Count > 0 || delta.Removed.Count > 0)
                await hubContext.Clients.All.SendAsync("SendMapDelta", delta, cancellationToken);
        }

        lastMapVersion = map.Version;
        lastMapBuildings = map.BuildingList.ToDictionary(building => building.NetworkId);
    }

    private static bool BuildingsEqual(MapBuildingStatus a, MapBuildingStatus b) =>
        a.Kind == b.Kind
        && a.Name == b.Name
        && a.District == b.District
        && a.X == b.X
        && a.Y == b.Y
        && a.Width == b.Width
        && a.Height == b.Height
        && a.EntryX == b.EntryX
        && a.EntryY == b.EntryY
        && a.OwnerNetworkId == b.OwnerNetworkId;
}

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

                await BroadcastIntentRejections(cancellationToken);
                await BroadcastPlayerLifecycle(cancellationToken);
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

    /// <summary>
    /// Drains post-admission intent feedback from the simulation and delivers it
    /// to every active connection for the player. Rejections for disconnected
    /// players are discarded; there is nobody left to notify.
    /// </summary>
    private async Task BroadcastIntentRejections(CancellationToken cancellationToken)
    {
        var processed = 0;
        while (processed < 128 && engine.TryDequeueIntentRejection(out var rejection))
        {
            processed++;
            var connections = registry.GetConnections(rejection.PlayerId);
            if (connections.Count == 0)
                continue;

            var message = new IntentRejected
            {
                PlayerId = rejection.PlayerId,
                Sequence = rejection.Sequence,
                Code = rejection.Code,
                Message = rejection.Message
            };

            foreach (var connectionId in connections)
            {
                await hubContext.Clients.Client(connectionId)
                    .SendAsync("IntentRejected", message, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Delivers player death/respawn notices to every connection for the player
    /// so a respawn prompt can follow them across tabs and reconnects.
    /// </summary>
    private async Task BroadcastPlayerLifecycle(CancellationToken cancellationToken)
    {
        var processed = 0;
        while (processed < 128 && engine.TryDequeuePlayerLifecycleNotice(out var notice))
        {
            processed++;
            var connections = registry.GetConnections(notice.PlayerId);
            if (connections.Count == 0)
                continue;

            foreach (var connectionId in connections)
            {
                await hubContext.Clients.Client(connectionId)
                    .SendAsync("PlayerLifecycle", notice, cancellationToken);
            }
        }
    }

    private async Task BroadcastWorld(WorldSnapshot snapshot, CancellationToken cancellationToken)
    {
        var baseDeltas = new Dictionary<long, WorldSnapshotDelta?>();

        foreach (var connectionId in registry.GetConnectionIds())
        {
            var acknowledged = replicationTracker.GetAcknowledgedTick(connectionId);
            var forceFull = replicationTracker.NeedsFullSnapshot(connectionId);
            if (acknowledged >= snapshot.Tick && !forceFull)
                continue;

            var viewport = viewports.TryGet(connectionId, out var active) ? active : (Viewport?)null;
            registry.TryGetPlayerId(connectionId, out var playerId);
            var ownSim = playerId is null ? 0 : engine.GetPlayerSimNetworkId(playerId);

            WorldSnapshotDelta? delta;
            if (forceFull)
            {
                var full = viewport is { } forcedViewport
                    ? AoiFilter.FilterSnapshotByViewport(snapshot, forcedViewport, ownSim)
                    : snapshot;
                delta = new WorldSnapshotDelta
                {
                    Tick = full.Tick,
                    SinceTick = 0,
                    Version = full.Version,
                    Full = true,
                    Sims = full.Sims,
                    Companies = full.Companies
                };
            }
            else
            {
                if (!baseDeltas.TryGetValue(acknowledged, out var baseDelta))
                {
                    baseDelta = engine.SnapshotHistory.BuildDelta(acknowledged);
                    baseDeltas[acknowledged] = baseDelta;
                }

                delta = baseDelta is null
                    ? null
                    : viewport is { } activeViewport
                        ? AoiFilter.FilterDeltaByViewport(baseDelta, activeViewport, ownSim)
                        : baseDelta;
            }

            if (delta is null)
                continue;

            if (delta.Full)
            {
                var full = new WorldSnapshot(delta.Tick, delta.Version, delta.Sims, delta.Companies);
                var bytes = ReplicationMetrics.EstimateBytes(full);
                replicationMetrics.Record(connectionId, full: true, bytes);
                await hubContext.Clients.Client(connectionId)
                    .SendAsync("SendSnapshot", full, cancellationToken);
                replicationTracker.MarkFullSnapshotSent(connectionId, full.Tick);
                continue;
            }

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

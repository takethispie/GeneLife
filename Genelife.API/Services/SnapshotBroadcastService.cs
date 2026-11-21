using Microsoft.AspNetCore.SignalR;

namespace Genelife.Api.Services;

public sealed class SnapshotBroadcastService : BackgroundService
{
    private readonly SimulationEngine engine;
    private readonly IHubContext<Hubs.WorldHub> hubContext;
    private readonly ILogger<SnapshotBroadcastService>? logger;

    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromMilliseconds(40);
    private long lastBroadcastedTick;

    public SnapshotBroadcastService(
        SimulationEngine engine,
        IHubContext<Hubs.WorldHub> hubContext,
        ILogger<SnapshotBroadcastService>? logger = null)
    {
        this.engine = engine;
        this.hubContext = hubContext;
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
                await hubContext.Clients.Group("all").SendAsync("SendSnapshot", snapshot, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Snapshot broadcast failed");
            }
        }
    }
}
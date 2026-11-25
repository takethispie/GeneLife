using Genelife.Api.Services;
using Genelife.Social;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Genelife.Api.Social;

/// <summary>
/// Removes expired reports and moderation audit rows on a background schedule.
/// It uses the simulation tick as a timestamp but never runs inside a tick.
/// </summary>
public sealed class ModerationRetentionService : BackgroundService
{
    private readonly ModerationService moderation;
    private readonly SimulationEngine engine;
    private readonly ModerationOptions options;
    private readonly TimeProvider time;
    private readonly ILogger<ModerationRetentionService>? logger;

    public ModerationRetentionService(
        ModerationService moderation,
        SimulationEngine engine,
        ModerationOptions options,
        TimeProvider time,
        ILogger<ModerationRetentionService>? logger = null)
    {
        this.moderation = moderation;
        this.engine = engine;
        this.options = options;
        this.time = time;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.RetentionSweepMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, time, stoppingToken);
                var removed = moderation.PurgeExpired(engine.CurrentTick);
                if (removed > 0)
                    logger?.LogInformation("Purged {Count} expired moderation row(s)", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Moderation retention sweep failed");
            }
        }
    }
}

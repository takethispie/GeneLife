using Genelife.Social;

namespace Genelife.Api.Social;

/// <summary>
/// Removes expired chat rows on a background schedule. It is independent of the
/// simulation tick loop and ignores a store without persistence.
/// </summary>
public sealed class ChatRetentionService : BackgroundService
{
    private readonly IChatStore store;
    private readonly ChatOptions options;
    private readonly TimeProvider time;
    private readonly ILogger<ChatRetentionService>? logger;

    public ChatRetentionService(
        IChatStore store,
        ChatOptions options,
        TimeProvider time,
        ILogger<ChatRetentionService>? logger = null)
    {
        this.store = store;
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
                var removed = await store.PurgeExpiredAsync(time.GetUtcNow(), stoppingToken);
                if (removed > 0)
                    logger?.LogInformation("Purged {Count} expired chat message(s)", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Chat retention sweep failed");
            }
        }
    }
}

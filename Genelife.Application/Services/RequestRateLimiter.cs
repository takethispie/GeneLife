using Microsoft.Extensions.Caching.Memory;

namespace Genelife.Api.Services;

/// <summary>
/// Small fixed-window limiter backed by <see cref="IMemoryCache"/>. Expired
/// partitions are evicted automatically by the cache.
/// </summary>
public sealed class RequestRateLimiter(IMemoryCache cache)
{
    public bool TryAcquire(string key, int maxPerWindow, TimeSpan window)
    {
        if (string.IsNullOrWhiteSpace(key) || maxPerWindow <= 0)
            return false;

        var counter = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Counter();
        });

        return counter is not null && Interlocked.Increment(ref counter.Count) <= maxPerWindow;
    }

    private sealed class Counter
    {
        public int Count;
    }
}

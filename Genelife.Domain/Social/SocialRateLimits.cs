namespace Genelife.Social;

public sealed record SocialRateLimitPolicy(
    int PerPlayer,
    int Global,
    int WindowSeconds)
{
    public static SocialRateLimitPolicy Disabled { get; } = new(0, 0, 1);

    public void Validate()
    {
        if (PerPlayer < 0 || Global < 0)
            throw new InvalidOperationException("Social rate limits cannot be negative.");
        if ((PerPlayer > 0 || Global > 0) && WindowSeconds < 1)
            throw new InvalidOperationException("Social rate limit windows must be positive.");
    }
}

/// <summary>
/// Shared fixed-window limiter for social surfaces. It enforces both a
/// per-player budget and a global per-world budget, and treats a refused
/// acquisition as consuming nothing.
/// </summary>
public sealed class SocialRateLimiter
{
    private readonly object gate = new();
    private readonly Dictionary<string, Window> windows = new(StringComparer.Ordinal);
    private readonly TimeProvider time;

    public SocialRateLimiter(TimeProvider time)
    {
        this.time = time;
    }

    public bool TryAcquire(
        string scope,
        string playerId,
        SocialRateLimitPolicy policy,
        out string notice)
    {
        notice = string.Empty;
        if (policy.PerPlayer <= 0 && policy.Global <= 0)
            return true;

        policy.Validate();
        var now = time.GetUtcNow();
        lock (gate)
        {
            var playerWindow = policy.PerPlayer > 0
                ? GetWindow($"{scope}|{playerId}", now, policy.WindowSeconds)
                : null;
            var globalWindow = policy.Global > 0
                ? GetWindow($"{scope}|*", now, policy.WindowSeconds)
                : null;

            if (playerWindow is not null && playerWindow.Count >= policy.PerPlayer)
            {
                notice = "You are doing that too quickly. Try again shortly.";
                return false;
            }

            if (globalWindow is not null && globalWindow.Count >= policy.Global)
            {
                notice = "The world is doing that too quickly. Try again shortly.";
                return false;
            }

            if (playerWindow is not null)
                playerWindow.Count++;
            if (globalWindow is not null)
                globalWindow.Count++;
        }

        return true;
    }

    private Window GetWindow(string key, DateTimeOffset now, int windowSeconds)
    {
        if (!windows.TryGetValue(key, out var window))
            windows[key] = window = new Window(now);
        else if (now - window.Start >= TimeSpan.FromSeconds(windowSeconds))
        {
            window.Start = now;
            window.Count = 0;
        }

        return window;
    }

    private sealed class Window(DateTimeOffset start)
    {
        public DateTimeOffset Start = start;
        public int Count;
    }
}

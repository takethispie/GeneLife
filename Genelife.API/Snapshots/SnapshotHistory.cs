namespace Genelife.Api.Snapshots;

/// <summary>
/// Bounded ring buffer of recent world snapshots. Used to compute per-client
/// deltas and to resume a reconnecting client from the last tick it
/// acknowledged. Retention is both tick-count and capacity bounded; a request
/// older than the retained window falls back to a full snapshot.
/// </summary>
public sealed class SnapshotHistory
{
    private readonly int capacity;
    private readonly WorldSnapshot?[] buffer;
    private readonly object sync = new();

    private long latestTick = -1;

    public SnapshotHistory(int capacity = 128)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        this.capacity = capacity;
        buffer = new WorldSnapshot?[capacity];
    }

    public long LatestTick
    {
        get { lock (sync) { return latestTick; } }
    }

    public void Record(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (sync)
        {
            buffer[Slot(snapshot.Tick)] = snapshot;

            if (snapshot.Tick > latestTick)
                latestTick = snapshot.Tick;
        }
    }

    public bool TryGet(long tick, out WorldSnapshot snapshot)
    {
        lock (sync)
        {
            return TryGetLocked(tick, out snapshot!);
        }
    }

    /// <summary>
    /// Builds a delta from <paramref name="sinceTick"/> to the latest snapshot.
    /// Returns a full snapshot when the requested tick is not retained (too old,
    /// unknown, or newer than what we have). Delta construction happens outside
    /// the lock because published snapshots are immutable.
    /// </summary>
    public WorldSnapshotDelta? BuildDelta(long sinceTick)
    {
        WorldSnapshot latest;
        WorldSnapshot from;

        lock (sync)
        {
            if (!TryGetLocked(latestTick, out latest!))
                return null;

            if (sinceTick <= 0 || sinceTick > latest.Tick || !TryGetLocked(sinceTick, out from!))
                return WorldSnapshotDelta.FromSnapshot(latest);
        }

        return WorldSnapshotDelta.Between(from, latest);
    }

    public void Clear()
    {
        lock (sync)
        {
            Array.Clear(buffer);
            latestTick = -1;
        }
    }

    private int Slot(long tick) => (int)(tick % capacity);

    private bool TryGetLocked(long tick, out WorldSnapshot snapshot)
    {
        snapshot = null!;
        if (tick < 0)
            return false;

        var candidate = buffer[Slot(tick)];
        if (candidate is null || candidate.Tick != tick)
            return false;

        snapshot = candidate;
        return true;
    }
}

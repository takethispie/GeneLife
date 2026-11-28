using Genelife.Api.DTOs;
using Genelife.Api.Services;

namespace Genelife.Api.Digest;

/// <summary>
/// The Town bell digest projection (GDD ch.08 section 8.5(6)). A bounded,
/// in-memory, per-player view over the outbound event channel: every event
/// touching a player's connected entities is grouped server-side by
/// <c>(eventKind, subjectEntityId)</c> within a rolling window, counting
/// occurrences and keeping the first and last tick.
///
/// Presentation contract (chapter 07 principle 5): the digest is a single pull
/// per session — it is never pushed as a toast, a pop-up over the world or a
/// modal interrupt. This service therefore exposes a read model only; it does
/// not talk to the hub.
///
/// No new persistence and no schema change: the buffer is process memory and is
/// rebuilt from the event channel after a restart.
/// </summary>
public sealed class TownBellDigestService : ISimulationEventSink
{
    private readonly object gate = new();
    private readonly LinkedList<DigestEvent> buffer = new();
    private readonly DigestOptions options;

    private long lastTick;
    private long sessionStartTick;

    public TownBellDigestService(DigestOptions options) => this.options = options;

    /// <summary>Latest tick observed on the channel (the digest's "as of").</summary>
    public long AsOfTick
    {
        get
        {
            lock (gate)
                return lastTick;
        }
    }

    /// <summary>Tick at which the current buffer began (world reset or first event).</summary>
    public long SessionStartTick
    {
        get
        {
            lock (gate)
                return sessionStartTick;
        }
    }

    /// <inheritdoc />
    public void OnEvent(object @event, long tick)
    {
        if (DigestEventClassifier.TryClassify(@event, tick, out var digest))
            Record(digest);
    }

    /// <summary>Records an already-classified digest event.</summary>
    public void Record(DigestEvent digest)
    {
        lock (gate)
        {
            if (buffer.Count == 0)
                sessionStartTick = digest.Tick;
            if (digest.Tick > lastTick)
                lastTick = digest.Tick;

            buffer.AddLast(digest);
            while (buffer.Count > options.BufferCapacity && buffer.First is { } oldest)
                buffer.RemoveFirst();
        }
    }

    /// <summary>Clears the buffer, e.g. when the world is regenerated.</summary>
    public void Reset(long tick)
    {
        lock (gate)
        {
            buffer.Clear();
            lastTick = tick;
            sessionStartTick = tick;
        }
    }

    /// <summary>Builds the digest for one player's connected-entity scope.</summary>
    public TownBellDigestDto Build(PlayerDigestScope scope, long? windowTicks = null)
    {
        var window = windowTicks is > 0 ? windowTicks.Value : options.WindowTicks;
        long asOf;
        lock (gate)
            asOf = lastTick;

        var from = Math.Max(0, asOf - window);
        var entries = Aggregate(
            e => scope.Contains(e.SubjectEntityType, e.SubjectEntityId),
            from,
            asOf);

        return new TownBellDigestDto(
            scope.PlayerId,
            scope.GenosNetworkId,
            asOf,
            from,
            window,
            $"last {window} ticks",
            entries.Count,
            entries);
    }

    /// <summary>
    /// Operator-plane view of the whole buffer (all entities, no scoping), for
    /// the admin surface. Not play-surface data (GDD ch.08 section 8.6).
    /// </summary>
    public DigestStatusDto BuildStatus()
    {
        long asOf;
        int buffered;
        long oldest;
        long newest;
        int distinct;
        lock (gate)
        {
            asOf = lastTick;
            buffered = buffer.Count;
            oldest = buffer.First?.Value.Tick ?? 0;
            newest = buffer.Last?.Value.Tick ?? 0;
            distinct = buffer.Select(e => e.SubjectRef).Distinct(StringComparer.Ordinal).Count();
        }

        var entries = Aggregate(static _ => true, long.MinValue, long.MaxValue);
        return new DigestStatusDto(asOf, buffered, distinct, oldest, newest, entries);
    }

    private List<DigestEntryDto> Aggregate(Func<DigestEvent, bool> filter, long fromTick, long toTick)
    {
        var groups = new Dictionary<string, DigestEntryDto>(StringComparer.Ordinal);
        lock (gate)
        {
            foreach (var e in buffer)
            {
                if (e.Tick < fromTick || e.Tick > toTick || !filter(e))
                    continue;

                if (groups.TryGetValue(e.GroupKey, out var current))
                {
                    groups[e.GroupKey] = current with
                    {
                        Count = current.Count + 1,
                        FirstTick = Math.Min(current.FirstTick, e.Tick),
                        LastTick = Math.Max(current.LastTick, e.Tick),
                    };
                }
                else
                {
                    groups[e.GroupKey] = new DigestEntryDto(
                        e.EventKind, e.SubjectEntityType, e.SubjectEntityId, 1, e.Tick, e.Tick);
                }
            }
        }

        return groups.Values
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.EventKind, StringComparer.Ordinal)
            .ThenBy(entry => entry.SubjectEntityId)
            .ToList();
    }
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Genelife.Api.Snapshots;

/// <summary>
/// Tracks the last world tick each transport connection acknowledged. Player
/// acknowledgements survive a disconnect so a new connection for the same
/// player can resume from the last applied tick when it is still inside the
/// <see cref="SnapshotHistory"/> retention window.
/// </summary>
public sealed class ReplicationTracker
{
    private readonly ConcurrentDictionary<string, long> connectionTicks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> playerTicks = new(StringComparer.Ordinal);

    /// <summary>Copies the player's last acknowledgement onto a new connection.</summary>
    public void RestoreForConnection(string connectionId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(playerId))
            return;

        if (playerTicks.TryGetValue(playerId, out var tick))
            connectionTicks[connectionId] = tick;
    }

    public void Acknowledge(string connectionId, string playerId, long tick)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return;

        connectionTicks[connectionId] = tick;
        if (!string.IsNullOrWhiteSpace(playerId))
            playerTicks[playerId] = tick;
    }

    public long GetAcknowledgedTick(string connectionId)
    {
        return connectionTicks.TryGetValue(connectionId, out var tick) ? tick : 0;
    }

    /// <summary>
    /// Drops the per-connection entry. The player-level acknowledgement is kept
    /// so a reconnect can resume from it.
    /// </summary>
    public void Remove(string connectionId)
    {
        connectionTicks.TryRemove(connectionId, out _);
    }
}

/// <summary>
/// Counts full snapshots and deltas along with their serialized payload sizes so
/// bandwidth per client can be observed without external telemetry.
/// </summary>
public sealed class ReplicationMetrics
{
    private readonly object sync = new();
    private readonly Dictionary<string, long> bytesByConnection = new(StringComparer.Ordinal);
    private long fullSnapshots;
    private long deltaSnapshots;
    private long fullBytes;
    private long deltaBytes;
    private long startTimestamp;
    private bool started;

    public void Record(string? connectionId, bool full, int bytes)
    {
        if (bytes < 0)
            bytes = 0;

        lock (sync)
        {
            if (!started)
            {
                started = true;
                startTimestamp = Stopwatch.GetTimestamp();
            }

            if (full)
            {
                fullSnapshots++;
                fullBytes += bytes;
            }
            else
            {
                deltaSnapshots++;
                deltaBytes += bytes;
            }

            if (!string.IsNullOrWhiteSpace(connectionId))
            {
                bytesByConnection.TryGetValue(connectionId, out var existing);
                bytesByConnection[connectionId] = existing + bytes;
            }
        }
    }

    public void Remove(string connectionId)
    {
        lock (sync)
        {
            bytesByConnection.Remove(connectionId);
        }
    }

    /// <summary>
    /// Serializes a payload with the same JSON contract used by the transport to
    /// report its wire size. Measurement failures degrade to zero bytes rather
    /// than breaking replication.
    /// </summary>
    public static int EstimateBytes(object payload)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(payload).Length;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return 0;
        }
    }

    public ReplicationMetricsSnapshot Snapshot()
    {
        lock (sync)
        {
            var elapsedSeconds = started
                ? Math.Max(1e-6, Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds)
                : 0;

            var totalBytes = fullBytes + deltaBytes;
            var clients = bytesByConnection.Count;

            return new ReplicationMetricsSnapshot(
                FullSnapshots: fullSnapshots,
                DeltaSnapshots: deltaSnapshots,
                FullBytes: fullBytes,
                DeltaBytes: deltaBytes,
                TotalBytes: totalBytes,
                ActiveClients: clients,
                BytesPerSecond: elapsedSeconds > 0 ? totalBytes / elapsedSeconds : 0,
                BytesPerClientPerSecond: elapsedSeconds > 0 && clients > 0
                    ? totalBytes / elapsedSeconds / clients
                    : 0);
        }
    }
}

public sealed record ReplicationMetricsSnapshot(
    long FullSnapshots,
    long DeltaSnapshots,
    long FullBytes,
    long DeltaBytes,
    long TotalBytes,
    int ActiveClients,
    double BytesPerSecond,
    double BytesPerClientPerSecond);


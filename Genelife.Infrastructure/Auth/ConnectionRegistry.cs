using System.Collections.Concurrent;

namespace Genelife.Api.Auth;

public sealed class ConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<string, string> connections = new(StringComparer.Ordinal);

    public void Register(string connectionId, string playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        connections[connectionId] = playerId;
    }

    public bool TryUnregister(string connectionId, out string? playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        return connections.TryRemove(connectionId, out playerId);
    }

    public bool TryGetPlayerId(string connectionId, out string? playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        return connections.TryGetValue(connectionId, out playerId);
    }

    public IReadOnlyCollection<string> GetConnections(string playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        return connections
            .Where(pair => string.Equals(pair.Value, playerId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();
    }

    public IReadOnlyCollection<string> GetPlayerIds()
    {
        return connections.Values.Distinct(StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyCollection<string> GetConnectionIds()
    {
        return connections.Keys.ToArray();
    }
}

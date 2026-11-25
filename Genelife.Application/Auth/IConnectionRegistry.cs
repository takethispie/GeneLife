namespace Genelife.Api.Auth;

/// <summary>
/// Maps live transport connections (for example, SignalR connection ids) to
/// authenticated players and back. Thread-safe for concurrent hub callbacks.
/// </summary>
public interface IConnectionRegistry
{
    void Register(string connectionId, string playerId);

    bool TryUnregister(string connectionId, out string? playerId);

    bool TryGetPlayerId(string connectionId, out string? playerId);

    IReadOnlyCollection<string> GetConnections(string playerId);

    IReadOnlyCollection<string> GetPlayerIds();

    IReadOnlyCollection<string> GetConnectionIds();
}

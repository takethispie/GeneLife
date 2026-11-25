using System.Collections.Concurrent;

namespace Genelife.Api.Snapshots;

public sealed class ViewportRegistry
{
    private readonly ConcurrentDictionary<string, Viewport> connectionViewports = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Viewport> playerViewports = new(StringComparer.Ordinal);

    public bool TrySet(string connectionId, Viewport viewport)
    {
        if (viewport.Radius <= 0)
            return false;
        connectionViewports[connectionId] = viewport;
        return true;
    }

    public bool TryGet(string connectionId, out Viewport viewport) =>
        connectionViewports.TryGetValue(connectionId, out viewport);

    public void SetForPlayer(string playerId, Viewport viewport)
    {
        if (!string.IsNullOrWhiteSpace(playerId))
            playerViewports[playerId] = viewport;
    }

    public bool TryGetForPlayer(string playerId, out Viewport viewport) =>
        playerViewports.TryGetValue(playerId, out viewport);

    public void Remove(string connectionId) => connectionViewports.TryRemove(connectionId, out _);

    public void RemovePlayer(string playerId)
    {
        if (!string.IsNullOrWhiteSpace(playerId))
            playerViewports.TryRemove(playerId, out _);
    }
}

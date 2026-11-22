using System.Collections.Concurrent;

namespace Genelife.Api.Snapshots;

public sealed class ViewportRegistry
{
    private readonly ConcurrentDictionary<string, Viewport> viewports = new(StringComparer.Ordinal);

    public bool TrySet(string connectionId, Viewport viewport)
    {
        if (viewport.Radius <= 0)
            return false;
        viewports[connectionId] = viewport;
        return true;
    }

    public bool TryGet(string connectionId, out Viewport viewport) =>
        viewports.TryGetValue(connectionId, out viewport);

    public void Remove(string connectionId) => viewports.TryRemove(connectionId, out _);
}

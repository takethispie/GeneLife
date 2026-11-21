using Arch.Core;
using Genelife.Components.Gen;

namespace Genelife.Api.Services;

public sealed class NetworkIdRegistry
{
    private readonly Dictionary<NetworkId, Entity> entitiesByNetworkId = new();
    private readonly Dictionary<Entity, NetworkId> networkIdsByEntity = new();
    private ulong nextValue = 1;

    public int Count => entitiesByNetworkId.Count;

    public NetworkId Allocate(Entity entity)
    {
        if (networkIdsByEntity.TryGetValue(entity, out var existing))
            return existing;

        var networkId = new NetworkId(nextValue++);
        entitiesByNetworkId[networkId] = entity;
        networkIdsByEntity[entity] = networkId;
        return networkId;
    }

    public bool TryGetEntity(NetworkId networkId, out Entity entity)
    {
        return entitiesByNetworkId.TryGetValue(networkId, out entity);
    }

    public bool TryGetNetworkId(Entity entity, out NetworkId networkId)
    {
        return networkIdsByEntity.TryGetValue(entity, out networkId);
    }

    public bool Remove(Entity entity)
    {
        if (!networkIdsByEntity.TryGetValue(entity, out var networkId))
            return false;

        networkIdsByEntity.Remove(entity);
        entitiesByNetworkId.Remove(networkId);
        return true;
    }

    public bool Remove(NetworkId networkId)
    {
        if (!entitiesByNetworkId.TryGetValue(networkId, out var entity))
            return false;

        entitiesByNetworkId.Remove(networkId);
        networkIdsByEntity.Remove(entity);
        return true;
    }

    public void Clear()
    {
        entitiesByNetworkId.Clear();
        networkIdsByEntity.Clear();
    }
}

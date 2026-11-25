using Arch.Core;
using Genelife.Components.Gen;

namespace Genelife.Api.Services;

public sealed class NetworkIdRegistry
{
    private readonly Dictionary<NetworkId, Entity> entitiesByNetworkId = new();
    private readonly Dictionary<Entity, NetworkId> networkIdsByEntity = new();
    private const ulong InfrastructureStart = 1_000_000;
    private ulong nextValue = 1;
    private ulong nextInfrastructure = InfrastructureStart;

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

    /// <summary>
    /// Allocates a high-range id for infrastructure children (utility networks,
    /// links, stock rows). These ids are persistable but never consume the
    /// normal id sequence, so adding infrastructure does not shift existing
    /// deterministic genos/household ids.
    /// </summary>
    public NetworkId AllocateInfrastructure(Entity entity)
    {
        if (networkIdsByEntity.TryGetValue(entity, out var existing))
            return existing;

        var networkId = new NetworkId(nextInfrastructure++);
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

    public void AllocateWithValue(Entity entity, NetworkId networkId)
    {
        if (entitiesByNetworkId.ContainsKey(networkId))
            throw new ArgumentException($"NetworkId {networkId.Value} is already mapped");
        entitiesByNetworkId[networkId] = entity;
        networkIdsByEntity[entity] = networkId;
        if (networkId.Value >= InfrastructureStart)
        {
            if (networkId.Value >= nextInfrastructure)
                nextInfrastructure = networkId.Value + 1;
        }
        else if (networkId.Value >= nextValue)
        {
            nextValue = networkId.Value + 1;
        }
    }

    public void Clear()
    {
        entitiesByNetworkId.Clear();
        networkIdsByEntity.Clear();
    }
}

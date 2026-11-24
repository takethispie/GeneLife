using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Gen;

namespace Genelife.UnitTests.Services;

public class NetworkIdRegistryTests
{
    [Fact]
    public void Allocate_ReturnsDistinctIdsForDistinctEntities()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();

        var first = registry.Allocate(world.Create());
        var second = registry.Allocate(world.Create());

        Assert.NotEqual(first, second);
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void Allocate_IsIdempotentForSameEntity()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var entity = world.Create();

        var first = registry.Allocate(entity);
        var second = registry.Allocate(entity);

        Assert.Equal(first, second);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void CreateDestroyRecreate_DoesNotReuseNetworkId()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();

        var first = world.Create();
        var firstId = registry.Allocate(first);
        world.Destroy(first);
        Assert.True(registry.Remove(first));

        var second = world.Create();
        var secondId = registry.Allocate(second);

        Assert.NotEqual(firstId, secondId);
        Assert.True(registry.TryGetEntity(secondId, out var resolved));
        Assert.Equal(second, resolved);
    }

    [Fact]
    public void TryGet_MapsBothDirections()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var entity = world.Create();
        var networkId = registry.Allocate(entity);

        Assert.True(registry.TryGetEntity(networkId, out var resolvedEntity));
        Assert.Equal(entity, resolvedEntity);

        Assert.True(registry.TryGetNetworkId(entity, out var resolvedId));
        Assert.Equal(networkId, resolvedId);
    }

    [Fact]
    public void Remove_CleansBothMaps()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var entity = world.Create();
        var networkId = registry.Allocate(entity);

        Assert.True(registry.Remove(entity));

        Assert.False(registry.TryGetEntity(networkId, out _));
        Assert.False(registry.TryGetNetworkId(entity, out _));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void RemoveByNetworkId_CleansBothMaps()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var entity = world.Create();
        var networkId = registry.Allocate(entity);

        Assert.True(registry.Remove(networkId));

        Assert.False(registry.TryGetEntity(networkId, out _));
        Assert.False(registry.TryGetNetworkId(entity, out _));
    }

    [Fact]
    public void Clear_RemovesMappingsWithoutReusingIds()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var networkId = registry.Allocate(world.Create());

        registry.Clear();

        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGetEntity(networkId, out _));

        var nextId = registry.Allocate(world.Create());
        Assert.NotEqual(networkId, nextId);
    }
}

using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class RelationshipDomainTests
{
    [Fact]
    public void PartnerPairCreatesMirroredEdgesAndIndexEntries()
    {
        var (world, index, service, a, b) = Setup();

        var edge = service.CreatePair(a, b, RelationshipKind.Partner, RelationshipSource.Seed, 12);

        Assert.True(world.Has<Relationship>(edge));
        Assert.Equal(2, index.ComponentCount);
        Assert.Equal([b], index.PartnersOf(a));
        Assert.Equal([a], index.PartnersOf(b));
        Assert.True(index.TryGetEdge(a, b, RelationshipKind.Partner, out _));
        Assert.True(index.TryGetEdge(b, a, RelationshipKind.Partner, out _));
    }

    [Fact]
    public void ParentPairCreatesParentAndChildMirror()
    {
        var (world, index, service, parent, child) = Setup();

        service.CreatePair(parent, child, RelationshipKind.Parent, RelationshipSource.Seed, 5);

        Assert.Equal([child], index.ChildrenOf(parent));
        Assert.Equal([parent], index.ParentsOf(child));
        Assert.True(index.TryGetEdge(parent, child, RelationshipKind.Parent, out _));
        Assert.True(index.TryGetEdge(child, parent, RelationshipKind.Child, out _));
        Assert.True(world.Has<Relationship>(index.Edges.First()));
    }

    [Fact]
    public void DuplicateAndSelfEdgesAreRejected()
    {
        var (_, _, service, a, b) = Setup();
        service.CreatePair(a, b, RelationshipKind.Partner, RelationshipSource.Seed, 1);

        Assert.False(service.TryCreatePair(a, b, RelationshipKind.Partner, RelationshipSource.Seed, 2,
            out _, out _, out var duplicate));
        Assert.Equal(RelationshipError.Duplicate, duplicate);

        Assert.False(service.TryCreatePair(a, a, RelationshipKind.Partner, RelationshipSource.Seed, 2,
            out _, out _, out var self));
        Assert.Equal(RelationshipError.SelfEdge, self);
    }

    [Fact]
    public void SecondActivePartnerIsRejected()
    {
        var (_, _, service, a, b, c) = SetupThree();
        service.CreatePair(a, b, RelationshipKind.Partner, RelationshipSource.Seed, 1);

        Assert.False(service.TryCreatePair(a, c, RelationshipKind.Partner, RelationshipSource.Seed, 2,
            out _, out _, out var error));
        Assert.Equal(RelationshipError.SecondPartner, error);
    }

    [Fact]
    public void AncestryCycleIsRejected()
    {
        var (_, _, service, a, b, c) = SetupThree();
        service.CreatePair(a, b, RelationshipKind.Parent, RelationshipSource.Seed, 1);

        Assert.False(service.TryCreatePair(b, a, RelationshipKind.Parent, RelationshipSource.Seed, 2,
            out _, out _, out var error));
        Assert.Equal(RelationshipError.AncestryCycle, error);
    }

    [Fact]
    public void EndingPartnershipKeepsHistoricalExPartnerEdges()
    {
        var (_, index, service, a, b) = Setup();
        service.CreatePair(a, b, RelationshipKind.Partner, RelationshipSource.Seed, 1);

        Assert.True(service.EndPartnership(a, b, 10));

        Assert.Empty(index.PartnersOf(a));
        Assert.Equal([b], index.ExPartnersOf(a));
        Assert.True(index.TryGetEdge(a, b, RelationshipKind.ExPartner, out _));
        Assert.True(index.TryGetEdge(b, a, RelationshipKind.ExPartner, out _));
    }

    [Fact]
    public void RemovingSimEdgesRemovesMirrorsAndLeavesNoStaleIndex()
    {
        var (world, index, service, a, b) = Setup();
        service.CreatePair(a, b, RelationshipKind.Partner, RelationshipSource.Seed, 1);

        Assert.Equal(1, service.RemoveEdgesForSim(a));

        Assert.Empty(index.Edges);
        Assert.Equal(0, index.ComponentCount);
        var components = 0;
        world.Query(in new QueryDescription().WithAll<Relationship>(), (ref Relationship _) => components++);
        Assert.Equal(0, components);
    }

    [Fact]
    public void RebuildRestoresTheIndexFromComponents()
    {
        var (world, index, service, a, b) = Setup();
        service.CreatePair(a, b, RelationshipKind.Partner, RelationshipSource.Seed, 1);

        index.Clear();
        Assert.Equal(0, index.ComponentCount);

        index.Rebuild(world);

        Assert.Equal(2, index.ComponentCount);
        Assert.Equal([b], index.PartnersOf(a));
    }

    [Fact]
    public void IndexRelationshipsAreOrderedByNetworkId()
    {
        var world = World.Create();
        var index = new RelationshipIndex { NetworkIdResolver = entity => world.Get<NetworkId>(entity).Value };
        var service = new RelationshipService(world, index);
        var parent = CreateSim(world, 1, new NetworkId(100));
        var older = CreateSim(world, 2, new NetworkId(20));
        var younger = CreateSim(world, 3, new NetworkId(30));

        service.CreatePair(parent, younger, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        service.CreatePair(parent, older, RelationshipKind.Parent, RelationshipSource.Seed, 1);

        Assert.Equal([older, younger], index.ChildrenOf(parent));

        index.Rebuild(world);
        Assert.Equal([older, younger], index.ChildrenOf(parent));
    }

    private static (World World, RelationshipIndex Index, RelationshipService Service, Entity A, Entity B)
        Setup()
    {
        var context = SetupThree();
        return (context.World, context.Index, context.Service, context.A, context.B);
    }

    private static (World World, RelationshipIndex Index, RelationshipService Service, Entity A, Entity B, Entity C)
        SetupThree()
    {
        var world = World.Create();
        var index = new RelationshipIndex();
        var service = new RelationshipService(world, index);
        var a = CreateSim(world, 1, new NetworkId(1));
        var b = CreateSim(world, 2, new NetworkId(2));
        var c = CreateSim(world, 3, new NetworkId(3));
        return (world, index, service, a, b, c);
    }

    private static Entity CreateSim(World world, int age, NetworkId networkId) =>
        world.Create(
            new GenName($"Sim {networkId.Value}"),
            new Alive(age),
            new Traits(),
            networkId);
}

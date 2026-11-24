using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class RelationshipInvariantTests
{
    [Fact]
    public void MissingMirrorIsReported()
    {
        var world = World.Create();
        var index = new RelationshipIndex();
        var first = world.Create(new Alive(30), new NetworkId(1));
        var second = world.Create(new Alive(31), new NetworkId(2));
        var edge = world.Create(new Relationship
        {
            From = first,
            To = second,
            Kind = RelationshipKind.Partner,
            Source = RelationshipSource.Seed,
            SinceTick = 1
        });
        world.Add(edge, new NetworkId(3));
        index.Rebuild(world);

        var issues = RelationshipInvariants.Verify(world, index);

        Assert.Contains(issues, issue => issue.Name == "relationship.mirror" && issue.Level == "Fail");
    }

    [Fact]
    public void StaleIndexEntryIsReported()
    {
        var world = World.Create();
        var index = new RelationshipIndex();
        var first = world.Create(new Alive(30), new NetworkId(1));
        var second = world.Create(new Alive(31), new NetworkId(2));
        index.OnCreated(Entity.Null, new Relationship
        {
            From = first,
            To = second,
            Kind = RelationshipKind.Partner,
            Source = RelationshipSource.Seed,
            SinceTick = 1
        });

        var issues = RelationshipInvariants.Verify(world, index);

        Assert.Contains(issues, issue => issue.Name == "relationship.index-count");
    }

    [Fact]
    public void MissingTraitsAreReportedByTraitInvariants()
    {
        var world = World.Create();
        world.Create(new Alive(30));

        var issues = TraitInvariants.Verify(world);

        Assert.Contains(issues, issue => issue.Name == "traits.missing");
    }
}

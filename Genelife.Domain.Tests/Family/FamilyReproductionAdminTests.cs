using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class FamilyReproductionAdminTests
{
    [Fact]
    public void PregnanciesChildrenAndGuardianshipsAreReadable()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong firstId = 0;
        ulong secondId = 0;
        ulong childId = 0;

        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            firstId = repository.Add("First", 30).Value;
            secondId = repository.Add("Second", 31).Value;
            childId = repository.Add("Baby", 0).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(firstId), out var first));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(secondId), out var second));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(childId), out var child));
            world.Get<Traits>(first) = new Traits { Sex = Sex.Female };
            world.Get<Traits>(second) = new Traits { Sex = Sex.Male };
            Assert.True(engine.PregnancyService!.TryStart(
                first, second, 100, 1_000, 90, PregnancySource.Conception,
                engine.ReproductionOptions, out _, out _));
            Assert.True(engine.RelationshipService!.TryCreatePair(
                first, second, RelationshipKind.Partner, RelationshipSource.Seed, 1,
                out _, out _, out _));
            Assert.True(engine.RelationshipService.TryCreatePair(
                second, child, RelationshipKind.Guardian, RelationshipSource.Admin, 1,
                out _, out _, out _));
            world.Add(child, new BirthRecord { PregnancyNetworkId = 1, BornTick = 1 });
            return 0;
        });

        var pregnancies = engine.BuildPregnancies();
        Assert.Single(pregnancies);
        Assert.Equal(100, pregnancies[0].ConceivedTick);
        Assert.Equal("Active", pregnancies[0].Status);

        var children = engine.BuildChildren();
        Assert.Single(children);
        Assert.Equal(childId, children[0].NetworkId);
        Assert.Equal(0, children[0].Age);

        var guardianships = engine.BuildGuardianships();
        Assert.Single(guardianships);
        Assert.Equal(secondId, guardianships[0].GuardianNetworkId);
        Assert.Equal(childId, guardianships[0].ChildNetworkId);
    }
}

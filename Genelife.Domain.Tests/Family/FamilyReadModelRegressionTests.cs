using Arch.Core;
using Genelife.Api.Commands;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class FamilyReadModelRegressionTests
{
    [Fact]
    public void UnpartneredGenosReportsNoPartner()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong genosId = 0;

        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            genosId = repository.Add("Solo", 30).Value;
            return 0;
        });

        var genos = Assert.Single(engine.BuildFamilyGenoses(take: 100).Genoses,
            row => row.NetworkId == genosId);

        Assert.False(genos.IsPaired);
        Assert.Null(genos.PartnerNetworkId);
        Assert.True(string.IsNullOrEmpty(genos.PartnerName));
    }

    [Fact]
    public void EndPartnershipMarksBothGenosesUnpaired()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var ids = CreatePair(engine);

        var forceId = engine.EnqueueAdminCommand(new ForcePartnershipCommand(
            Guid.NewGuid(), "tester", ids.First, ids.Second, false, "test-force"));
        engine.Tick(1f / 20f);
        Assert.True(engine.GetCommandResult(forceId)!.Accepted);

        var paired = engine.BuildFamilyGenoses(take: 100).Genoses;
        Assert.True(paired.Single(genos => genos.NetworkId == ids.First).IsPaired);
        Assert.True(paired.Single(genos => genos.NetworkId == ids.Second).IsPaired);

        var endId = engine.EnqueueAdminCommand(new EndPartnershipCommand(
            Guid.NewGuid(), "tester", ids.First, ids.Second, "test-end"));
        engine.Tick(1f / 20f);
        Assert.True(engine.GetCommandResult(endId)!.Accepted);

        var unpaired = engine.BuildFamilyGenoses(take: 100).Genoses;
        foreach (var id in new[] { ids.First, ids.Second })
        {
            var genos = unpaired.Single(row => row.NetworkId == id);
            Assert.False(genos.IsPaired);
            Assert.Null(genos.PartnerNetworkId);
            Assert.True(string.IsNullOrEmpty(genos.PartnerName));
        }
    }

    [Fact]
    public void ChildWithoutGuardianReportsNoGuardian()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong childId = 0;

        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            childId = repository.Add("Unattended Child", 8).Value;
            return 0;
        });

        var child = Assert.Single(engine.BuildFamilyGenoses(take: 100).Genoses,
            row => row.NetworkId == childId);

        Assert.Null(child.GuardianNetworkId);
    }

    [Fact]
    public void EnsureGuardianAssignsStateCareWhenNoCandidateExists()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var relationships = new RelationshipIndex();
        var service = new RelationshipService(world, relationships);
        var guardianship = new GuardianshipService(
            world, economy, relationships, service, new GuardianshipOptions());
        var minor = world.Create(new GenName("Orphan"), new Alive(8));

        Assert.True(guardianship.EnsureGuardian(minor, 10, out var guardian, out var stateCare));

        Assert.True(stateCare);
        Assert.Equal(Entity.Null, guardian);
        Assert.True(world.Has<StateCare>(minor));
    }

    private static (ulong First, ulong Second) CreatePair(SimulationEngine engine)
    {
        ulong firstId = 0;
        ulong secondId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            firstId = repository.Add("First", 30).Value;
            secondId = repository.Add("Second", 31).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(firstId), out var first));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(secondId), out var second));
            world.Get<Traits>(first) = new Traits { Sex = Sex.Female };
            world.Get<Traits>(second) = new Traits { Sex = Sex.Male };
            return 0;
        });

        return (firstId, secondId);
    }
}

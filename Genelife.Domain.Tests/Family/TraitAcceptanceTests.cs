using Arch.Core;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class TraitAcceptanceTests
{
    [Fact]
    public void GeneratedPopulationHasOneValidTraitSetPerLivingSim()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(200, seed: 4242);
            return 0;
        });

        var traits = ReadLivingTraits(engine, out var living);
        Assert.Equal(200, living);
        Assert.Equal(living, traits.Count);
        Assert.All(traits.Values, value => Assert.True(TraitValidation.IsValid(value)));

        var distribution = engine.ExecuteWithWorld(world => TraitDistribution.Calculate(world));
        Assert.Equal(living, distribution.WithTraits);
        Assert.InRange(distribution.FemaleShare, 0.42m, 0.58m);
        Assert.Empty(TraitInvariants.Verify(engine.World!));
    }

    [Fact]
    public void SameSeedProducesIdenticalTraitsForIdenticalNetworkIds()
    {
        var first = GenerateTraits(seed: 99, sims: 60);
        var second = GenerateTraits(seed: 99, sims: 60);

        Assert.Equal(first.Count, second.Count);
        foreach (var (networkId, expected) in first)
            Assert.Equal(expected, second[networkId]);

        var different = GenerateTraits(seed: 100, sims: 60);
        Assert.Contains(first, pair => different[pair.Key] != pair.Value);
    }

    [Fact]
    public void PlayerAndAdminCreationPathsAddTraits()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        var playerId = engine.EnsurePlayerSim("alice");
        Assert.NotEqual(0UL, playerId);

        var adminId = engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, engine.NetworkIds, engine.Economy, engine.MapContext);
            return repository.Add("Admin Sim", 30).Value;
        });

        foreach (var id in new[] { playerId, adminId })
        {
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(id), out var entity));
            Assert.True(engine.World!.Has<Traits>(entity));
            Assert.True(TraitValidation.IsValid(engine.World.Get<Traits>(entity)));
        }
    }

    private static Dictionary<ulong, Traits> GenerateTraits(int seed, int sims)
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(sims, seed: seed);
            return 0;
        });

        return ReadLivingTraits(engine, out _);
    }

    private static Dictionary<ulong, Traits> ReadLivingTraits(SimulationEngine engine, out int living)
    {
        var traits = new Dictionary<ulong, Traits>();
        var count = 0;
        var world = engine.World!;
        world.Query(in new QueryDescription().WithAll<Alive, NetworkId>().WithNone<Dead>(),
            (Entity entity, ref Alive _, ref NetworkId id) =>
            {
                count++;
                if (world.Has<Traits>(entity))
                    traits[id.Value] = world.Get<Traits>(entity);
            });
        living = count;
        return traits;
    }
}

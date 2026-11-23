using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class SeedFamilyTests
{
    [Fact]
    public void SeededPopulationHasOppositeSexPartnersAndParentEdges()
    {
        using var engine = new SimulationEngine();
        engine.Initialize(relationshipOptions: new RelationshipOptions { SeedPartnershipShare = 0.6m });

        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(120, seed: 7);
            return 0;
        });

        var world = engine.World!;
        var partners = 0;
        var parents = 0;
        world.Query(in new QueryDescription().WithAll<Relationship>(),
            (ref Relationship relationship) =>
            {
                if (relationship.Kind == RelationshipKind.Partner)
                    partners++;
                if (relationship.Kind == RelationshipKind.Parent)
                    parents++;
            });

        Assert.True(partners > 0, "expected seeded partner edges");
        Assert.Equal(0, partners % 2);
        Assert.True(parents > 0, "expected seeded parent edges");
        Assert.Empty(RelationshipInvariants.Verify(world, engine.Relationships));

        var paired = 0;
        world.Query(in new QueryDescription().WithAll<Alive, Traits, NetworkId>().WithNone<Dead>(),
            (Entity entity, ref Alive _, ref Traits _, ref NetworkId _) =>
            {
                var partnerList = engine.Relationships.PartnersOf(entity);
                if (partnerList.Count == 0)
                    return;
                paired++;
                var partner = partnerList[0];
                Assert.True(world.Has<Traits>(partner));
                Assert.NotEqual(world.Get<Traits>(entity).Sex, world.Get<Traits>(partner).Sex);
            });

        Assert.True(paired > 0);
        Assert.Equal(partners, paired);
    }

    [Fact]
    public void ZeroSeedPartnershipShareCreatesNoPartners()
    {
        using var engine = new SimulationEngine();
        engine.Initialize(relationshipOptions: new RelationshipOptions { SeedPartnershipShare = 0m });
        Generate(engine, 60, seed: 9);

        Assert.Equal(0, CountKind(engine.World!, RelationshipKind.Partner));
    }

    [Fact]
    public void FullSeedPartnershipSharePairsMostEligibleAdults()
    {
        using var engine = new SimulationEngine();
        engine.Initialize(relationshipOptions: new RelationshipOptions { SeedPartnershipShare = 1m });
        Generate(engine, 80, seed: 11);

        var adults = 0;
        var paired = 0;
        var world = engine.World!;
        world.Query(in new QueryDescription().WithAll<Alive, Traits>().WithNone<Dead>(),
            (Entity entity, ref Alive alive, ref Traits _) =>
            {
                if (alive.Age < 18)
                    return;
                adults++;
                if (engine.Relationships.PartnersOf(entity).Count > 0)
                    paired++;
            });

        Assert.True(adults > 0);
        Assert.True(paired / (decimal)adults >= 0.6m,
            $"paired {paired}/{adults} adults at full seed share");
    }

    [Fact]
    public void SameSeedProducesTheSamePartnerPairs()
    {
        var first = PartnerPairs(seed: 13);
        var second = PartnerPairs(seed: 13);

        Assert.Equal(first, second);
    }

    private static List<(ulong, ulong)> PartnerPairs(int seed)
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        Generate(engine, 80, seed);
        var pairs = new List<(ulong, ulong)>();
        foreach (var edge in engine.Relationships.Edges)
        {
            var relationship = engine.World!.Get<Relationship>(edge);
            if (relationship.Kind != RelationshipKind.Partner)
                continue;
            var from = engine.World.Get<NetworkId>(relationship.From).Value;
            var to = engine.World.Get<NetworkId>(relationship.To).Value;
            if (from < to)
                pairs.Add((from, to));
        }

        pairs.Sort();
        return pairs;
    }

    private static void Generate(SimulationEngine engine, int sims, int seed) =>
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

    private static int CountKind(World world, RelationshipKind kind)
    {
        var count = 0;
        world.Query(in new QueryDescription().WithAll<Relationship>(),
            (ref Relationship relationship) =>
            {
                if (relationship.Kind == kind)
                    count++;
            });
        return count;
    }
}

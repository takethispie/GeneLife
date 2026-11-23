using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class FamilyAdminTests
{
    [Fact]
    public void FamilyStatusRelationshipsAndAttractionExplainTheWorld()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong firstId = 0;
        ulong secondId = 0;
        ulong thirdId = 0;
        ulong fourthId = 0;

        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, engine.NetworkIds, engine.Economy);
            firstId = repository.Add("First", 30).Value;
            secondId = repository.Add("Second", 31).Value;
            thirdId = repository.Add("Third", 29).Value;
            fourthId = repository.Add("Fourth", 28).Value;

            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(firstId), out var first));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(secondId), out var second));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(thirdId), out var third));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(fourthId), out var fourth));
            world.Get<Traits>(first) = new Traits { Sex = Sex.Female };
            world.Get<Traits>(second) = new Traits { Sex = Sex.Male };
            world.Get<Traits>(third) = new Traits { Sex = Sex.Male };
            world.Get<Traits>(fourth) = new Traits { Sex = Sex.Female };

            Assert.True(engine.RelationshipService!.TryCreatePair(
                first, second, RelationshipKind.Partner, RelationshipSource.Seed, 1,
                out _, out _, out _));
            return 0;
        });

        var status = engine.BuildFamilyStatus();
        Assert.Equal(4, status.Population);
        Assert.Equal(4, status.Adults);
        Assert.Equal(2, status.PairedSims);
        Assert.Equal(1, status.Partnerships);
        Assert.False(status.SameSexPairingAllowed);
        Assert.DoesNotContain(status.Health, check => check.Level == "Fail");
        Assert.Contains(status.Health, check => check.Name == "relationships" && check.Level == "Pass");

        var relationships = engine.BuildFamilyRelationships(simId: firstId);
        Assert.Equal(2, relationships.Total);
        Assert.All(relationships.Relationships, relationship =>
            Assert.True(relationship.FromSimId == firstId || relationship.ToSimId == firstId));

        var paired = engine.BuildAttractionExplanation(firstId, secondId);
        Assert.False(paired.Eligible);
        Assert.Equal("has-partner", paired.ReasonCode);

        var eligible = engine.BuildAttractionExplanation(thirdId, fourthId);
        Assert.True(eligible.Eligible);
        Assert.True(eligible.Score > 0);
        Assert.Equal(6, eligible.Components.Count);
        Assert.Equal(eligible.Score, eligible.Components.Sum(component => component.Points));

        var missing = engine.BuildAttractionExplanation(thirdId, 0);
        Assert.Equal("not-found", missing.ReasonCode);
    }

    [Fact]
    public void FamilyReadModelPagesFilterAndExplain()
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
            generator.Generate(60, seed: 21);
            return 0;
        });

        var status = engine.BuildFamilyStatus();
        Assert.Equal(status.Population, status.Traits.WithTraits);
        Assert.Equal(0, status.Traits.MissingTraits);
        Assert.NotEmpty(status.Traits.Categories);
        Assert.NotEmpty(status.Households);
        Assert.False(status.Projection.IsStale);
        Assert.True(status.Performance.CandidateCap > 0);
        Assert.DoesNotContain(status.Health, check => check.Level == "Fail");

        var all = engine.BuildFamilySims(take: 200);
        Assert.Equal(status.Population, all.Total);
        Assert.True(all.Sims.Count <= 200);

        var page = engine.BuildFamilySims(take: 10, skip: 5);
        Assert.Equal(10, page.Sims.Count);
        Assert.Equal(all.Sims.Skip(5).Take(10).Select(sim => sim.NetworkId), page.Sims.Select(sim => sim.NetworkId));

        var partnered = engine.BuildFamilySims(take: 200, partnered: true);
        Assert.All(partnered.Sims, sim => Assert.True(sim.IsPaired));
        Assert.True(partnered.Total > 0);

        var withChildren = engine.BuildFamilySims(take: 200, hasChildren: true);
        Assert.All(withChildren.Sims, sim => Assert.True(sim.HasChildren));

        var householdId = status.Households[0].NetworkId;
        var householdSims = engine.BuildFamilySims(take: 200, householdId: householdId);
        Assert.All(householdSims.Sims, sim => Assert.Equal(householdId, sim.HouseholdNetworkId));

        var detail = engine.BuildFamilySim(partnered.Sims[0].NetworkId);
        Assert.NotNull(detail);
        Assert.Equal(partnered.Sims[0].NetworkId, detail!.Sim.NetworkId);
        Assert.NotEmpty(detail.Relationships);
        Assert.NotNull(detail.Household);

        var pregnancies = engine.BuildPregnancyPage(activeOnly: true, take: 10);
        Assert.True(pregnancies.Total >= pregnancies.Pregnancies.Count);
        var children = engine.BuildChildPage(ageBand: "school", take: 10);
        Assert.All(children.Children, child => Assert.InRange(child.Age, 5, 12));
        if (children.Children.Count > 0)
            Assert.NotNull(engine.BuildChild(children.Children[0].NetworkId));

        var traits = engine.BuildTraitDistribution();
        Assert.Equal(status.Population, traits.WithTraits);
        Assert.Equal(8, traits.Categories.Count);
    }
}

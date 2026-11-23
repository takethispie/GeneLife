using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Systems;

namespace Genelife.UnitTests.Family;

public class CandidateGenerationTests
{
    [Fact]
    public void CandidateSourcesAreLocalAndIneligibleNeighboursAreExplained()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var index = new RelationshipIndex();
        var options = new RelationshipOptions();
        var generator = new CandidateGenerator(world, economy, index, options);
        var spatial = SpatialIndex.CreateDefault();

        var sim = CreateSim(world, 30, Sex.Female, 1, 0, 0);
        var householdPartner = CreateSim(world, 28, Sex.Male, 2, 0, 0);
        var sameSexNeighbour = CreateSim(world, 29, Sex.Female, 3, 1, 0);
        var farNeighbour = CreateSim(world, 32, Sex.Male, 4, 3, 4);

        var household = world.Create(new Household { Head = sim });
        world.Add(sim, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(householdPartner, new HouseholdMember(household, HouseholdRole.Partner));
        economy.Housing.AddMember(sim, household);
        economy.Housing.AddMember(householdPartner, household);

        spatial.Add(sim, world.Get<Location>(sim).Position);
        spatial.Add(householdPartner, world.Get<Location>(householdPartner).Position);
        spatial.Add(sameSexNeighbour, world.Get<Location>(sameSexNeighbour).Position);
        spatial.Add(farNeighbour, world.Get<Location>(farNeighbour).Position);

        var result = generator.Generate(
            sim,
            spatial,
            new Dictionary<Entity, List<Entity>>(),
            new RelationshipCache(),
            tick: 0);

        Assert.Contains(householdPartner, result.Candidates);
        Assert.Contains(farNeighbour, result.Candidates);
        Assert.DoesNotContain(sameSexNeighbour, result.Candidates);
        Assert.Contains(result.Rejections,
            rejection => rejection.Sim == sameSexNeighbour && rejection.Reason == "same-sex-not-allowed");
    }

    [Fact]
    public void CandidateListIsCappedDeterministically()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var index = new RelationshipIndex();
        var options = new RelationshipOptions { MaxCandidatesPerSim = 2 };
        var generator = new CandidateGenerator(world, economy, index, options);
        var spatial = SpatialIndex.CreateDefault();
        var sim = CreateSim(world, 30, Sex.Female, 1, 0, 0);
        var household = world.Create(new Household { Head = sim });
        world.Add(sim, new HouseholdMember(household, HouseholdRole.Head));
        economy.Housing.AddMember(sim, household);

        for (ulong i = 2; i <= 6; i++)
        {
            var candidate = CreateSim(world, 28 + (int)i % 4, Sex.Male, i, 0, 0);
            world.Add(candidate, new HouseholdMember(household, HouseholdRole.Relative));
            economy.Housing.AddMember(candidate, household);
        }

        var result = generator.Generate(
            sim,
            spatial,
            new Dictionary<Entity, List<Entity>>(),
            new RelationshipCache(),
            tick: 0);

        Assert.Equal(2, result.Candidates.Count);
    }

    private static Entity CreateSim(
        World world,
        int age,
        Sex sex,
        ulong networkId,
        float x,
        float y) =>
        world.Create(
            new GenName($"Sim {networkId}"),
            new Alive(age),
            new Traits { Sex = sex },
            new NetworkId(networkId),
            new Location(x, y));

}

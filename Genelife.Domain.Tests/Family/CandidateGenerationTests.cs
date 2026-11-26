using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Map;
using Genelife.Application.Systems;

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

        var genos = CreateGenos(world, 30, Sex.Female, 1, 0, 0);
        var householdPartner = CreateGenos(world, 28, Sex.Male, 2, 0, 0);
        var sameSexNeighbour = CreateGenos(world, 29, Sex.Female, 3, 1, 0);
        var farNeighbour = CreateGenos(world, 32, Sex.Male, 4, 3, 4);

        var household = world.Create(new Household { Head = genos });
        world.Add(genos, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(householdPartner, new HouseholdMember(household, HouseholdRole.Partner));
        economy.Housing.AddMember(genos, household);
        economy.Housing.AddMember(householdPartner, household);

        spatial.Add(genos, world.Get<Location>(genos).Position);
        spatial.Add(householdPartner, world.Get<Location>(householdPartner).Position);
        spatial.Add(sameSexNeighbour, world.Get<Location>(sameSexNeighbour).Position);
        spatial.Add(farNeighbour, world.Get<Location>(farNeighbour).Position);

        var result = generator.Generate(
            genos,
            spatial,
            new Dictionary<Entity, List<Entity>>(),
            new RelationshipCache(),
            tick: 0);

        Assert.Contains(householdPartner, result.Candidates);
        Assert.Contains(farNeighbour, result.Candidates);
        Assert.DoesNotContain(sameSexNeighbour, result.Candidates);
        Assert.Contains(result.Rejections,
            rejection => rejection.Genos == sameSexNeighbour && rejection.Reason == "same-sex-not-allowed");
    }

    [Fact]
    public void CandidateListIsCappedDeterministically()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var index = new RelationshipIndex();
        var options = new RelationshipOptions { MaxCandidatesPerGenos = 2 };
        var generator = new CandidateGenerator(world, economy, index, options);
        var spatial = SpatialIndex.CreateDefault();
        var genos = CreateGenos(world, 30, Sex.Female, 1, 0, 0);
        var household = world.Create(new Household { Head = genos });
        world.Add(genos, new HouseholdMember(household, HouseholdRole.Head));
        economy.Housing.AddMember(genos, household);

        for (ulong i = 2; i <= 6; i++)
        {
            var candidate = CreateGenos(world, 28 + (int)i % 4, Sex.Male, i, 0, 0);
            world.Add(candidate, new HouseholdMember(household, HouseholdRole.Relative));
            economy.Housing.AddMember(candidate, household);
        }

        var result = generator.Generate(
            genos,
            spatial,
            new Dictionary<Entity, List<Entity>>(),
            new RelationshipCache(),
            tick: 0);

        Assert.Equal(2, result.Candidates.Count);
    }

    private static Entity CreateGenos(
        World world,
        int age,
        Sex sex,
        ulong networkId,
        float x,
        float y) =>
        world.Create(
            new GenName($"Genos {networkId}"),
            new Alive(age),
            new Traits { Sex = sex },
            new NetworkId(networkId),
            new Location(x, y));

}

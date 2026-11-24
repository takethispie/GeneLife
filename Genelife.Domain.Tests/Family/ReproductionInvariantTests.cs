using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class ReproductionInvariantTests
{
    [Fact]
    public void DuplicateActivePregnancyIsAFailure()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var carrying = world.Create(new Alive(30), new Traits(), new NetworkId(1));
        var other = world.Create(new Alive(31), new Traits(), new NetworkId(2));
        for (var i = 0; i < 2; i++)
            world.Create(new Pregnancy
            {
                CarryingParent = carrying,
                OtherParent = other,
                ConceivedTick = 0,
                DueTick = 100,
                Status = PregnancyStatus.Active,
                ChildrenExpected = 1
            });

        var issues = ReproductionInvariants.Verify(world, new RelationshipIndex(), economy);

        Assert.Contains(issues, issue => issue.Name == "pregnancy.duplicate-active" && issue.Level == "Fail");
    }

    [Fact]
    public void ChildEmploymentIsAFailure()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        world.Create(
            new Alive(10),
            new Employee(Entity.Null, Entity.Null, "Test", Money.FromUnits(10m)));

        var issues = ReproductionInvariants.Verify(world, new RelationshipIndex(), economy);

        Assert.Contains(issues, issue => issue.Name == "child.employed" && issue.Level == "Fail");
    }

    [Fact]
    public void NewbornWithoutParentEdgesIsAFailure()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        world.Create(
            new Alive(0),
            new GenName("Foundling"),
            new Traits(),
            new BirthRecord { PregnancyNetworkId = 1, BornTick = 1 },
            new HouseholdMember(Entity.Null, HouseholdRole.Child));

        var issues = ReproductionInvariants.Verify(world, new RelationshipIndex(), economy);

        Assert.Contains(issues, issue => issue.Name == "newborn.parent-edges" && issue.Level == "Fail");
    }

    [Fact]
    public void ParentEdgeInvariantAppliesToMinorsButNotAdults()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var workStartAge = economy.Life.Policy.WorkStartAge;
        var adult = world.Create(
            new Alive(workStartAge),
            new GenName("Adult"),
            new Traits(),
            new BirthRecord { PregnancyNetworkId = 1, BornTick = 1 });
        var minor = world.Create(
            new Alive(workStartAge - 1),
            new GenName("Minor"),
            new Traits(),
            new BirthRecord { PregnancyNetworkId = 2, BornTick = 2 });

        var issues = ReproductionInvariants.Verify(world, new RelationshipIndex(), economy);
        var parentEdgeIssues = issues
            .Where(issue => issue.Name == "newborn.parent-edges" && issue.Level == "Fail")
            .ToList();

        Assert.DoesNotContain(parentEdgeIssues,
            issue => issue.Detail.StartsWith($"Newborn {adult.Id} "));
        Assert.Single(parentEdgeIssues);
        Assert.Contains(parentEdgeIssues,
            issue => issue.Detail.StartsWith($"Newborn {minor.Id} "));
    }

    [Fact]
    public void OvercrowdedHouseholdIsAWarning()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var parent = world.Create(new Alive(35), new Wallet(Money.Zero));
        var child = world.Create(new Alive(8), new Wallet(Money.Zero));
        var household = world.Create(new Household { Head = parent, Tenure = Tenure.Renter, MemberCount = 2 });
        world.Add(parent, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(child, new HouseholdMember(household, HouseholdRole.Child));
        economy.Housing.AddMember(parent, household);
        economy.Housing.AddMember(child, household);
        var property = world.Create(new Property("Central", "Central", Entity.Null, 1));
        var unit = world.Create(new HousingUnit(property, "1", HousingTier.Basic, 1, Money.FromUnits(100m)));
        economy.Housing.MoveIn(unit, household);

        var issues = ReproductionInvariants.Verify(world, new RelationshipIndex(), economy);

        Assert.Contains(issues, issue => issue.Name == "housing.overcrowded" && issue.Level == "Warn");
    }

    [Fact]
    public void OvercrowdedHouseholdIsReportedOncePerHousehold()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var parent = world.Create(new Alive(35), new Wallet(Money.Zero));
        var firstChild = world.Create(new Alive(8), new Wallet(Money.Zero));
        var secondChild = world.Create(new Alive(6), new Wallet(Money.Zero));
        var household = world.Create(new Household { Head = parent, Tenure = Tenure.Renter, MemberCount = 3 });
        world.Add(parent, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(firstChild, new HouseholdMember(household, HouseholdRole.Child));
        world.Add(secondChild, new HouseholdMember(household, HouseholdRole.Child));
        economy.Housing.AddMember(parent, household);
        economy.Housing.AddMember(firstChild, household);
        economy.Housing.AddMember(secondChild, household);
        var property = world.Create(new Property("Central", "Central", Entity.Null, 1));
        var unit = world.Create(new HousingUnit(property, "1", HousingTier.Basic, 1, Money.FromUnits(100m)));
        economy.Housing.MoveIn(unit, household);

        var issues = ReproductionInvariants.Verify(world, new RelationshipIndex(), economy);

        Assert.Single(issues, issue => issue.Name == "housing.overcrowded" && issue.Level == "Warn");
    }
}

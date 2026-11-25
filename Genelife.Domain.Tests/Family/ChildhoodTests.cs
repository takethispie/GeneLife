using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Housing;
using Genelife.Life;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class ChildhoodTests
{
    [Theory]
    [InlineData(0, true, false)]
    [InlineData(5, true, false)]
    [InlineData(6, true, true)]
    [InlineData(17, true, true)]
    [InlineData(18, false, false)]
    public void AgeGatesMatchThePolicy(int age, bool dependent, bool schoolAge)
    {
        var world = World.Create();
        var genos = world.Create(new Alive(age));
        if (age < 18)
            world.Add(genos, new DependentChild());
        var policy = LifeStagePolicy.Default;

        Assert.Equal(dependent, ChildhoodPolicy.IsDependentChild(world, genos, policy));
        Assert.Equal(schoolAge, ChildhoodPolicy.IsSchoolAge(world, genos, policy));
    }

    [Fact]
    public void AdulthoodClearsTheDependentMarkerAndAddsAJobSeeker()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var genos = world.Create(
            new GenName("Graduate"),
            new Alive(18),
            new SkillProfile(),
            new DependentChild(),
            new Genelife.Components.Life.Lifecycle(
                LifeStage.Education,
                18L * economy.Clock.TicksPerYear,
                0));

        new StageProgressionSystem(world, economy).Update(100);

        Assert.False(world.Has<DependentChild>(genos));
        Assert.Equal(LifeStage.Work, world.Get<Lifecycle>(genos).Stage);
        Assert.True(world.Has<JobSeeker>(genos));
    }

    [Fact]
    public void AdultChildMovesOutUnderPolicyAndRegistryStaysConsistent()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        HousingBootstrap.CreateProperty(world, economy, "Central", HousingTier.Basic, 4, 0.8f, 0.5f, 0);
        var allocator = new HousingAllocator(world, economy);
        var parent = world.Create(new GenName("Parent"), new Alive(45), new Wallet(Money.FromUnits(500m)));
        var child = world.Create(
            new GenName("Child"),
            new Alive(18),
            new Wallet(Money.Zero),
            new DependentChild());
        var household = world.Create(new Household { Head = parent, Tenure = Tenure.Renter, MemberCount = 2 });
        world.Add(household, new NetworkId(1));
        world.Add(parent, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(child, new HouseholdMember(household, HouseholdRole.Child));
        economy.Housing.AddMember(parent, household);
        economy.Housing.AddMember(child, household);
        Assert.True(allocator.TryAssignBest(household, 0));

        var relationships = new RelationshipIndex();
        new FamilyHouseholdSystem(
            world,
            economy,
            relationships,
            new RelationshipOptions { CohabitOnPairing = false },
            new FamilyHousingPolicy { AdultChildMoveOutShare = 1m })
            .Update(0);

        var childHousehold = economy.Housing.HouseholdOf(child);
        Assert.NotEqual(household, childHousehold);
        Assert.Equal(1, economy.Housing.MemberCount(household));
        Assert.Equal(HouseholdRole.Head, world.Get<HouseholdMember>(child).Role);
        Assert.False(world.Has<DependentChild>(child));
        Assert.NotEqual(Entity.Null, economy.Housing.UnitOf(childHousehold));
        Assert.Empty(economy.Housing.Verify());
    }
}

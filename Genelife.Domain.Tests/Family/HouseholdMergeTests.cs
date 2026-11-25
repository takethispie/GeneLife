using Arch.Core;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Housing;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class HouseholdMergeTests
{
    [Fact]
    public void PartnersMergeIntoOneHouseholdAndReleaseTheSourceUnit()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        HousingBootstrap.CreateProperty(world, economy, "Central", HousingTier.Basic, 4, 0.8f, 0.5f, 0);
        var allocator = new HousingAllocator(world, economy);

        var first = CreateGenos(world, 1, Money.FromUnits(800));
        var second = CreateGenos(world, 2, Money.FromUnits(900));
        var firstHousehold = CreateHousehold(world, economy, allocator, first, 1);
        var secondHousehold = CreateHousehold(world, economy, allocator, second, 2);
        var firstUnit = economy.Housing.UnitOf(firstHousehold);
        var secondUnit = economy.Housing.UnitOf(secondHousehold);
        var firstWallet = world.Get<Wallet>(first).Balance;

        var service = new HouseholdMergeService(world, economy);
        var merged = service.TryMergePartners(
            first, second,
            out var fromHousehold, out var toHousehold,
            out var fromNetworkId, out var toNetworkId);

        Assert.True(merged);
        Assert.Equal(firstHousehold, toHousehold);
        Assert.Equal(secondHousehold, fromHousehold);
        Assert.NotEqual(0UL, fromNetworkId);
        Assert.NotEqual(0UL, toNetworkId);
        Assert.False(world.IsAlive(secondHousehold));
        Assert.Equal(2, economy.Housing.MemberCount(firstHousehold));
        Assert.Equal(firstUnit, economy.Housing.UnitOf(firstHousehold));
        Assert.True(world.Get<HousingUnit>(secondUnit).IsVacant);
        Assert.Equal(firstWallet, world.Get<Wallet>(first).Balance);
        Assert.Empty(economy.Housing.Verify());
    }

    [Fact]
    public void MergeIsRejectedWhenTargetCapacityWouldBeExceeded()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        HousingBootstrap.CreateProperty(world, economy, "Central", HousingTier.Basic, 4, 0.8f, 0.5f, 0);
        var allocator = new HousingAllocator(world, economy);

        var first = CreateGenos(world, 1, Money.FromUnits(800));
        var second = CreateGenos(world, 2, Money.FromUnits(800));
        var extraA = CreateGenos(world, 3, Money.FromUnits(800));
        var extraB = CreateGenos(world, 4, Money.FromUnits(800));
        var firstHousehold = CreateHousehold(world, economy, allocator, first, 1);
        var secondHousehold = CreateHousehold(world, economy, allocator, second, 2);

        foreach (var extra in new[] { extraA, extraB })
        {
            world.Add(extra, new HouseholdMember(firstHousehold, HouseholdRole.Relative));
            economy.Housing.AddMember(extra, firstHousehold);
        }

        var service = new HouseholdMergeService(world, economy);
        var merged = service.TryMergePartners(
            first, second,
            out _, out _, out _, out _);

        Assert.False(merged);
        Assert.True(world.IsAlive(secondHousehold));
        Assert.Equal(3, economy.Housing.MemberCount(firstHousehold));
    }

    [Fact]
    public void SplitCreatesANewHouseholdAndKeepsRegistryConsistent()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        HousingBootstrap.CreateProperty(world, economy, "Central", HousingTier.Basic, 4, 0.8f, 0.5f, 0);
        var allocator = new HousingAllocator(world, economy);

        var first = CreateGenos(world, 1, Money.FromUnits(800));
        var second = CreateGenos(world, 2, Money.FromUnits(900));
        var firstHousehold = CreateHousehold(world, economy, allocator, first, 1);
        var secondHousehold = CreateHousehold(world, economy, allocator, second, 2);
        var service = new HouseholdMergeService(world, economy);
        Assert.True(service.TryMergePartners(first, second, out _, out _, out _, out _));

        Assert.True(service.TrySplit(second, 10, out var splitHousehold));

        Assert.True(world.IsAlive(splitHousehold));
        Assert.Equal(1, economy.Housing.MemberCount(firstHousehold));
        Assert.Equal(1, economy.Housing.MemberCount(splitHousehold));
        Assert.NotEqual(Entity.Null, economy.Housing.UnitOf(splitHousehold));
        Assert.Empty(economy.Housing.Verify());
    }

    private static Entity CreateGenos(World world, ulong networkId, Money cash) =>
        world.Create(
            new GenName($"Genos {networkId}"),
            new Alive(30),
            new Traits { Sex = networkId % 2 == 0 ? Sex.Male : Sex.Female },
            new NetworkId(networkId),
            new Wallet(cash));

    private static Entity CreateHousehold(
        World world,
        EconomyContext economy,
        HousingAllocator allocator,
        Entity head,
        ulong networkId)
    {
        var household = world.Create(new Household
        {
            Head = head,
            Tenure = Tenure.Renter,
            MemberCount = 1
        });
        world.Add(household, new NetworkId(networkId));
        world.Add(head, new HouseholdMember(household, HouseholdRole.Head));
        economy.Housing.AddMember(head, household);
        Assert.True(allocator.TryAssignBest(household, 0));
        return household;
    }
}

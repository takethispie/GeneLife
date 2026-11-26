using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Economy;

/// <summary>
/// Consumption is charged per genos, but cash belongs to the household. A genos whose own
/// wallet has just been emptied by rent or a utility bill must still be fed out of the
/// household's cash instead of having the whole month recorded as unmet demand.
/// </summary>
public class HouseholdPoolingTests
{
    [Fact]
    public void GenosWithAnEmptyWallet_IsFedFromHouseholdCash()
    {
        var (world, economy, broke, spouse) = HouseholdWithOneBrokePartner();

        new ConsumerSpendingSystem(world, economy, interval: 1).Update(100);

        ref var state = ref world.Get<ConsumptionState>(broke);
        Assert.True(state.FoodPurchased, "the household could pay, so food should have been bought");
        Assert.True(world.Get<Wallet>(spouse).Balance < Money.FromUnits(1_000m),
            "the spouse's wallet should carry the purchase");
    }

    [Fact]
    public void GenosWithAnEmptyWallet_StaysUnfedWhenTheWholeHouseholdIsBroke()
    {
        var (world, economy, broke, spouse) = HouseholdWithOneBrokePartner(spouseBalance: Money.Zero);

        new ConsumerSpendingSystem(world, economy, interval: 1).Update(100);

        Assert.False(world.Get<ConsumptionState>(broke).FoodPurchased);
        Assert.Equal(Money.Zero, world.Get<Wallet>(spouse).Balance);
    }

    private static (World World, EconomyContext Economy, Entity Broke, Entity Spouse) HouseholdWithOneBrokePartner(
        Money? spouseBalance = null)
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = world.Create(new CompanyName("Shop"), CompanyAccount.Create(Money.Zero, 0));
        world.Create(new Office("HQ", company, 10));

        var spouse = world.Create(
            new GenName("Spouse"),
            new Alive(35),
            new Wallet(spouseBalance ?? Money.FromUnits(1_000m)));
        var broke = world.Create(
            new GenName("Broke"),
            new Alive(35),
            new Wallet(Money.Zero));

        var household = world.Create(new Household { Head = spouse, Tenure = Tenure.Renter, MemberCount = 2 });
        world.Add(spouse, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(broke, new HouseholdMember(household, HouseholdRole.Partner));
        economy.Housing.AddMember(spouse, household);
        economy.Housing.AddMember(broke, household);

        return (world, economy, broke, spouse);
    }
}
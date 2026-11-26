using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class ChildCostTests
{
    [Fact]
    public void DependentChildSpendingIsPaidByTheGuardian()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = world.Create(new CompanyName("Shop"), CompanyAccount.Create(Money.Zero, 0));
        world.Create(new Office("HQ", company, 10));
        var guardian = world.Create(
            new GenName("Guardian"),
            new Alive(35),
            new Wallet(Money.FromUnits(1_000m)));
        var child = world.Create(
            new GenName("Child"),
            new Alive(4),
            new Wallet(Money.Zero),
            new DependentChild());
        var household = world.Create(new Household { Head = guardian, Tenure = Tenure.Renter, MemberCount = 2 });
        world.Add(guardian, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(child, new HouseholdMember(household, HouseholdRole.Child));
        economy.Housing.AddMember(guardian, household);
        economy.Housing.AddMember(child, household);
        var before = economy.TotalMoneySupply();

        new ConsumerSpendingSystem(world, economy, interval: 1).Update(100);

        Assert.Equal(Money.Zero, world.Get<Wallet>(child).Balance);
        Assert.True(world.Get<Wallet>(guardian).Balance < Money.FromUnits(1_000m));
        Assert.Contains(company, economy.PendingRevenue.Keys);
        Assert.Equal(before, economy.TotalMoneySupply());
    }
}

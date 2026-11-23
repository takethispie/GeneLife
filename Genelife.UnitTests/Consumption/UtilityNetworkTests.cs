using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Consumption;
using Genelife.Consumption;
using Genelife.Economy;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Consumption;

public class UtilityNetworkTests
{
    [Fact]
    public void LoadStateAndPrioritySheddingAreDeterministic()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var ids = new NetworkIdRegistry();
        var options = new UtilityOptions
        {
            InitialWaterProduction = 100,
            InitialWaterCapacity = 100,
            InitialElectricityProduction = 150,
            InitialElectricityCapacity = 150
        };
        var service = new UtilityNetworkService(world, economy, entity => ids.Allocate(entity), options);
        service.EnsureNetworks();

        var company = world.Create(new CompanyName("Acme"), CompanyAccount.Create(Money.FromUnits(10_000m), 0));
        ids.Allocate(company);
        for (var i = 0; i < 8; i++)
        {
            var office = world.Create(new Office($"Office {i}", company, capacity: 10));
            ids.Allocate(office);
        }
        service.EnsureBuildingLinks();
        service.Recalculate(100);

        var water = service.FindNetwork(UtilityService.Water);
        var waterNetwork = world.Get<UtilityNetwork>(water);
        Assert.True(waterNetwork.Load <= waterNetwork.Capacity);
        Assert.NotEqual(UtilityNetworkState.Normal, waterNetwork.State);

        var connected = 0;
        world.Query(in new QueryDescription().WithAll<BuildingServiceLink, ServiceLinkOwner>(),
            (ref BuildingServiceLink link, ref ServiceLinkOwner _) =>
            {
                if (link.Service == UtilityService.Water && link.Connected) connected++;
            });
        Assert.True(connected < 8);

        economy.Government.Balance = Money.FromUnits(100m);
        var expanded = service.ExpandCapacity(
            UtilityService.Water,
            units: 1_000,
            cost: Money.FromUnits(10m),
            tick: 101);
        Assert.True(expanded);
        service.Recalculate(200);
        Assert.Equal(UtilityNetworkState.Normal, world.Get<UtilityNetwork>(water).State);
    }

    [Fact]
    public void ExpandingWithoutGovernmentFundsFailsAndMovesNoMoney()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var ids = new NetworkIdRegistry();
        var service = new UtilityNetworkService(world, economy, entity => ids.Allocate(entity));
        service.EnsureNetworks();
        var before = economy.TotalMoneySupply();
        economy.Government.Balance = Money.Zero;

        Assert.False(service.ExpandCapacity(UtilityService.Water, 100, Money.FromUnits(50m), 1));
        Assert.Equal(before, economy.TotalMoneySupply());
    }
}

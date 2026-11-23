using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Components.Consumption;
using Genelife.Economy;
using Genelife.Housing;

namespace Genelife.UnitTests.Persistence;

public class ConsumptionPersistenceTests
{
    [Fact]
    public void NetworksLinksMetersAndStockRoundTripExactly()
    {
        using var engine = new SimulationEngine();
        engine.Initialize(new EconomyOptions(), new HousingOptions());
        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world,
                engine.NetworkIds,
                engine.Economy!,
                engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(60, seed: 21);
            return 0;
        });

        for (var i = 0; i < 110; i++)
            engine.Tick(1f / 20f);

        var first = engine.CaptureState(engine.NetworkIds);
        Assert.Equal(14, first.SchemaVersion);
        Assert.Equal(2, first.UtilityNetworks.Count);
        Assert.NotEmpty(first.ServiceLinks);
        Assert.NotEmpty(first.HouseholdMeters);
        Assert.NotEmpty(first.StoreInventories);
        Assert.True(first.GroceryCatalogVersion > 0);

        var networkBefore = first.UtilityNetworks
            .OrderBy(network => network.Service)
            .Select(network => (network.Service, network.Capacity, network.State))
            .ToList();
        var meterBefore = first.HouseholdMeters
            .OrderBy(meter => meter.HouseholdNetworkId)
            .Select(meter => (meter.HouseholdNetworkId, meter.WaterUsage, meter.ElectricityUsage, meter.ArrearsCents))
            .ToList();
        var stockBefore = first.StoreInventories
            .OrderBy(stock => stock.StoreNetworkId)
            .ThenBy(stock => stock.ItemIndex)
            .Select(stock => (stock.StoreNetworkId, stock.ItemIndex, stock.Stock, stock.CurrentPriceCents))
            .ToList();

        engine.Shutdown();

        using var restored = new SimulationEngine();
        restored.Restore(first, restored.NetworkIds);

        var second = restored.CaptureState(restored.NetworkIds);
        Assert.Equal(networkBefore, second.UtilityNetworks
            .OrderBy(network => network.Service)
            .Select(network => (network.Service, network.Capacity, network.State))
            .ToList());
        Assert.Equal(meterBefore, second.HouseholdMeters
            .OrderBy(meter => meter.HouseholdNetworkId)
            .Select(meter => (meter.HouseholdNetworkId, meter.WaterUsage, meter.ElectricityUsage, meter.ArrearsCents))
            .ToList());
        Assert.Equal(stockBefore, second.StoreInventories
            .OrderBy(stock => stock.StoreNetworkId)
            .ThenBy(stock => stock.ItemIndex)
            .Select(stock => (stock.StoreNetworkId, stock.ItemIndex, stock.Stock, stock.CurrentPriceCents))
            .ToList());

        var third = restored.CaptureState(restored.NetworkIds);
        Assert.Equal(second.ServiceLinks.Count, third.ServiceLinks.Count);
        Assert.Equal(second.HouseholdMeters.Count, third.HouseholdMeters.Count);
        Assert.Equal(second.StoreInventories.Count, third.StoreInventories.Count);

        restored.Shutdown();
        Assert.NotEmpty(meterBefore);
    }
}

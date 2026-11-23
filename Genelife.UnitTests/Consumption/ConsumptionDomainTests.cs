using System.Reflection;
using System.Text.Json;
using Genelife.Components.Consumption;
using Genelife.Consumption;
using Genelife.Values;

namespace Genelife.UnitTests.Consumption;

public class ConsumptionDomainTests
{
    [Fact]
    public void UtilityEnumsAndComponentsRoundTrip()
    {
        var network = new UtilityNetwork
        {
            Service = UtilityService.Electricity,
            Production = 100,
            Capacity = 120,
            Load = 80,
            Reserve = 40,
            State = UtilityNetworkState.Strained
        };
        var meter = new HouseholdMeter
        {
            WaterUsage = 4,
            ElectricityUsage = 6,
            WaterLevel = ServiceLevel.Reduced,
            ElectricityLevel = ServiceLevel.Full,
            Arrears = Money.FromUnits(12m),
            LastReadingTick = 300,
            ConsecutivePoorMonths = 1,
            LastCutOffTick = 200
        };
        var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
        var json = JsonSerializer.Serialize(network, jsonOptions);
        Assert.Equal(network, JsonSerializer.Deserialize<UtilityNetwork>(json, jsonOptions));
        var meterJson = JsonSerializer.Serialize(meter, jsonOptions);
        Assert.Equal(meter, JsonSerializer.Deserialize<HouseholdMeter>(meterJson, jsonOptions));
        Assert.Empty(ConsumptionValidation.Validate(network));
        Assert.Empty(ConsumptionValidation.Validate(meter));
    }

    [Fact]
    public void ValidationRejectsNegativeUsageStockAndArrears()
    {
        var meter = new HouseholdMeter
        {
            WaterUsage = -1,
            ElectricityUsage = -1,
            Arrears = Money.FromCents(-1),
            WaterLevel = ServiceLevel.Full,
            ElectricityLevel = ServiceLevel.Full
        };
        var errors = ConsumptionValidation.Validate(meter);
        Assert.Contains(errors, error => error.Contains("WaterUsage"));
        Assert.Contains(errors, error => error.Contains("ElectricityUsage"));
        Assert.Contains(errors, error => error.Contains("Arrears"));

        var inventory = new StoreInventory
        {
            ItemIndex = 0,
            Stock = -1,
            CurrentPrice = Money.FromUnits(1m)
        };
        Assert.Contains(ConsumptionValidation.Validate(inventory), error => error.Contains("Stock"));
        Assert.Contains(
            ConsumptionValidation.Validate(inventory with { ItemIndex = 999 }),
            error => error.Contains("ItemIndex"));
    }

    [Fact]
    public void CatalogIsDeterministicVersionedAndSizedForStructs()
    {
        Assert.Equal(1, GroceryCatalog.Version);
        Assert.Equal(7, GroceryCatalog.Items.Count);
        Assert.Equal("bread", GroceryCatalog.Items[0].Id);
        Assert.All(GroceryCatalog.Items, item =>
        {
            Assert.True(GroceryCatalog.IndexOf(item.Id) >= 0);
            Assert.True(item.BasePrice.IsPositive);
            Assert.True(item.ShelfLifeTicks > 0);
            Assert.True(item.Nutrition >= 0);
        });
        Assert.True(System.Runtime.InteropServices.Marshal.SizeOf<UtilityNetwork>() <= 32);
        Assert.DoesNotContain(
            typeof(StoreInventory).GetFields(BindingFlags.Public | BindingFlags.Instance),
            field => field.FieldType == typeof(float) || field.FieldType == typeof(decimal));
    }

    [Fact]
    public void TariffAndBillsAreNonNegativeAndDeterministic()
    {
        var tariff = UtilityTariff.FromOptions(UtilityOptions.Default);
        Assert.Equal(tariff.WaterBill(4), tariff.WaterBill(4));
        Assert.True(tariff.WaterBill(0).IsPositive);
        Assert.True(tariff.ElectricityBill(0).IsPositive);
        Assert.Equal(tariff.WaterBase, tariff.WaterBill(-10));
    }
}

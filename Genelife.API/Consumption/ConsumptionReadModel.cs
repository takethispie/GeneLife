using Genelife.Api.DTOs;
using Genelife.Components.Consumption;
using Genelife.Components.Gen;
using Genelife.Components.Housing;

namespace Genelife.Api.Consumption;

public static class ConsumptionReadModel
{
    public static UtilityNetworkStatus Network(Arch.Core.World world, Arch.Core.Entity entity)
    {
        var network = world.Get<UtilityNetwork>(entity);
        return new UtilityNetworkStatus(
            world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0,
            network.Service.ToString(),
            network.Production,
            network.Capacity,
            network.Load,
            network.Reserve,
            network.State.ToString());
    }

    public static ConsumptionHouseholdStatus Household(
        Arch.Core.World world,
        Arch.Core.Entity entity,
        Arch.Core.Entity unit,
        int occupants)
    {
        var meter = world.Has<HouseholdMeter>(entity)
            ? world.Get<HouseholdMeter>(entity)
            : default;
        var household = world.Get<Household>(entity);
        return new ConsumptionHouseholdStatus(
            world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0,
            unit != Arch.Core.Entity.Null && world.Has<NetworkId>(unit) ? world.Get<NetworkId>(unit).Value : 0,
            occupants,
            meter.WaterUsage,
            meter.ElectricityUsage,
            meter.WaterLevel.ToString(),
            meter.ElectricityLevel.ToString(),
            meter.Arrears.Cents,
            household.RentArrears.Cents,
            household.NoUtilities,
            meter.ConsecutivePoorMonths);
    }
}

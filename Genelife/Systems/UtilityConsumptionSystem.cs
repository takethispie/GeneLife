using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Consumption;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Consumption;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Monthly utility load, service-level and meter reconciliation. It runs before
/// rent so the bill uses this month's readings and service levels.
/// </summary>
public sealed class UtilityConsumptionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly UtilityOptions options;
    private readonly UtilityNetworkService networks;
    private readonly int interval;

    public UtilityConsumptionSystem(
        World world,
        EconomyContext economy,
        Func<Entity, NetworkId> allocateNetworkId,
        UtilityOptions? options = null,
        int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.options = options ?? UtilityOptions.Default;
        networks = new UtilityNetworkService(world, economy, allocateNetworkId, this.options);
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "UtilityConsumption";
    public int Order => 470;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        networks.EnsureNetworks();
        networks.EnsureBuildingLinks();
        networks.Recalculate(tick);

        var waterNetwork = networks.FindNetwork(UtilityService.Water);
        var electricityNetwork = networks.FindNetwork(UtilityService.Electricity);
        var waterState = waterNetwork == Entity.Null || !world.Has<UtilityNetwork>(waterNetwork)
            ? UtilityNetworkState.Offline
            : world.Get<UtilityNetwork>(waterNetwork).State;
        var electricityState = electricityNetwork == Entity.Null || !world.Has<UtilityNetwork>(electricityNetwork)
            ? UtilityNetworkState.Offline
            : world.Get<UtilityNetwork>(electricityNetwork).State;

        var linkIndex = new Dictionary<(Entity, UtilityService), BuildingServiceLink>();
        world.Query(in new QueryDescription().WithAll<BuildingServiceLink, ServiceLinkOwner>(),
            (ref BuildingServiceLink link, ref ServiceLinkOwner owner) =>
                linkIndex[(owner.Building, link.Service)] = link);

        var households = new List<(Entity Entity, Household Household)>();
        world.Query(in new QueryDescription().WithAll<Household>(),
            (Entity entity, ref Household household) => households.Add((entity, household)));
        households.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

        foreach (var (entity, household) in households)
        {
            if (!world.IsAlive(entity) || !world.Has<Household>(entity))
                continue;

            var meter = world.Has<HouseholdMeter>(entity)
                ? world.Get<HouseholdMeter>(entity)
                : new HouseholdMeter
                {
                    WaterLevel = ServiceLevel.Full,
                    ElectricityLevel = ServiceLevel.Full
                };

            var occupants = Math.Max(1, economy.Housing.MemberCount(entity));
            var unit = household.Unit;
            var property = unit != Entity.Null && world.IsAlive(unit) && world.Has<HousingUnit>(unit)
                ? world.Get<HousingUnit>(unit).Property
                : Entity.Null;

            var waterLevel = LevelFor(property, UtilityService.Water, waterState, meter.ConsecutivePoorMonths, linkIndex);
            var electricityLevel = LevelFor(property, UtilityService.Electricity, electricityState, meter.ConsecutivePoorMonths, linkIndex);

            if (meter.WaterLevel != waterLevel)
                economy.Emit(new UtilityServiceCutOff(
                    GetNetworkId(entity), UtilityService.Water.ToString(), waterLevel.ToString(), CauseFor(waterState), tick));
            if (meter.ElectricityLevel != electricityLevel)
                economy.Emit(new UtilityServiceCutOff(
                    GetNetworkId(entity), UtilityService.Electricity.ToString(), electricityLevel.ToString(), CauseFor(electricityState), tick));

            meter.WaterUsage = Usage(occupants, options.WaterUsagePerOccupant, waterLevel);
            meter.ElectricityUsage = Usage(occupants, options.ElectricityUsagePerOccupant, electricityLevel);
            meter.WaterLevel = waterLevel;
            meter.ElectricityLevel = electricityLevel;
            meter.LastReadingTick = tick;

            if (world.Has<HouseholdMeter>(entity))
                world.Get<HouseholdMeter>(entity) = meter;
            else
                world.Add(entity, meter);

            var data = world.Get<Household>(entity);
            data.UtilityArrears = meter.Arrears;
            data.NoUtilities = waterLevel != ServiceLevel.Full || electricityLevel != ServiceLevel.Full;
            world.Get<Household>(entity) = data;
        }
    }

    private ServiceLevel LevelFor(
        Entity property,
        UtilityService service,
        UtilityNetworkState networkState,
        int poorMonths,
        IReadOnlyDictionary<(Entity, UtilityService), BuildingServiceLink> links)
    {
        if (property == Entity.Null || !links.TryGetValue((property, service), out var link) || !link.Connected)
            return ServiceLevel.CutOff;
        if (networkState == UtilityNetworkState.Offline)
            return ServiceLevel.CutOff;
        if (poorMonths >= options.CutOffArrearsMonths)
            return ServiceLevel.CutOff;
        if (poorMonths >= options.ReducedArrearsMonths)
            return ServiceLevel.Reduced;
        if (networkState is UtilityNetworkState.Overloaded or UtilityNetworkState.Strained)
            return ServiceLevel.Reduced;
        return ServiceLevel.Full;
    }

    private static int Usage(int occupants, int perOccupant, ServiceLevel level) => level switch
    {
        ServiceLevel.CutOff => 0,
        ServiceLevel.Reduced => Math.Max(0, occupants * perOccupant / 2),
        _ => Math.Max(0, occupants * perOccupant)
    };

    private static string CauseFor(UtilityNetworkState state) => state switch
    {
        UtilityNetworkState.Offline => "network_offline",
        UtilityNetworkState.Overloaded => "network_overloaded",
        UtilityNetworkState.Strained => "network_strained",
        _ => "meter"
    };

    private ulong GetNetworkId(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}

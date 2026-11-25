using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Consumption;
using Genelife.Components.Company;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Map;
using Genelife.Enums;
using Genelife.Economy;
using Genelife.Map;
using Genelife.Values;

namespace Genelife.Consumption;

/// <summary>
/// One authoritative network per service. Building links are child entities so
/// a building can hold one link per service without fixed-size component arrays.
/// </summary>
public sealed class UtilityNetworkService
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly UtilityOptions options;
    private readonly Func<Entity, NetworkId> allocateNetworkId;

    public UtilityNetworkService(
        World world,
        EconomyContext economy,
        Func<Entity, NetworkId> allocateNetworkId,
        UtilityOptions? options = null)
    {
        this.world = world;
        this.economy = economy;
        this.allocateNetworkId = allocateNetworkId;
        this.options = options ?? UtilityOptions.Default;
    }

    public void EnsureNetworks()
    {
        EnsureNetwork(UtilityService.Water, options.InitialWaterProduction, options.InitialWaterCapacity);
        EnsureNetwork(UtilityService.Electricity, options.InitialElectricityProduction, options.InitialElectricityCapacity);
    }

    public Entity FindNetwork(UtilityService service)
    {
        var found = Entity.Null;
        world.Query(in new QueryDescription().WithAll<UtilityNetwork>(),
            (Entity entity, ref UtilityNetwork network) =>
            {
                if (found == Entity.Null && network.Service == service)
                    found = entity;
            });
        return found;
    }

    public void EnsureBuildingLinks()
    {
        var water = FindNetwork(UtilityService.Water);
        var electricity = FindNetwork(UtilityService.Electricity);
        if (water == Entity.Null || electricity == Entity.Null)
            return;

        var existing = new HashSet<(Entity, UtilityService)>();
        world.Query(in new QueryDescription().WithAll<BuildingServiceLink, ServiceLinkOwner>(),
            (ref BuildingServiceLink link, ref ServiceLinkOwner owner) =>
                existing.Add((owner.Building, link.Service)));

        foreach (var (building, kind, waterDemand, electricityDemand) in Buildings())
        {
            if (!existing.Contains((building, UtilityService.Water)))
                EnsureLink(building, water, UtilityService.Water, waterDemand, PriorityFor(kind));
            if (!existing.Contains((building, UtilityService.Electricity)))
                EnsureLink(building, electricity, UtilityService.Electricity, electricityDemand, PriorityFor(kind));
        }
    }

    public void Recalculate(long tick)
    {
        foreach (var service in new[] { UtilityService.Water, UtilityService.Electricity })
        {
            var networkEntity = FindNetwork(service);
            if (networkEntity == Entity.Null || !world.Has<UtilityNetwork>(networkEntity))
                continue;

            // Every cycle starts connected; the priority pass disconnects only
            // what the current capacity cannot serve.
            var links = new List<(Entity Entity, BuildingServiceLink Link)>();
            world.Query(in new QueryDescription().WithAll<BuildingServiceLink, ServiceLinkOwner>(),
                (Entity entity, ref BuildingServiceLink link, ref ServiceLinkOwner _) =>
                {
                    if (link.Service != service)
                        return;
                    link.Connected = true;
                    world.Get<BuildingServiceLink>(entity) = link;
                    links.Add((entity, link));
                });

            var network = world.Get<UtilityNetwork>(networkEntity);
            var previousState = network.State;
            var load = 0L;
            foreach (var (_, link) in links)
                load += link.Demand;

            network.Load = (int)Math.Min(int.MaxValue, load);
            network.Reserve = Math.Max(0, network.Capacity - network.Load);
            network.State = Classify(network);

            if (network.State is UtilityNetworkState.Overloaded or UtilityNetworkState.Offline)
            {
                // Commercial/discretionary links are shed before residential
                // and civic links. Lower priority numbers are more important.
                links.Sort((a, b) =>
                {
                    var byPriority = b.Link.Priority.CompareTo(a.Link.Priority);
                    return byPriority != 0 ? byPriority : a.Entity.Id.CompareTo(b.Entity.Id);
                });

                foreach (var (entity, link) in links)
                {
                    if (network.Load <= network.Capacity)
                        break;
                    var updated = link;
                    updated.Connected = false;
                    world.Get<BuildingServiceLink>(entity) = updated;
                    network.Load = Math.Max(0, network.Load - link.Demand);
                }
                network.Reserve = Math.Max(0, network.Capacity - network.Load);
            }

            world.Get<UtilityNetwork>(networkEntity) = network;
            if (previousState != network.State)
            {
                economy.Emit(new Genelife.Messages.Events.UtilityNetworkStateChanged(
                    GetNetworkId(networkEntity),
                    service.ToString(),
                    network.Production,
                    network.Capacity,
                    network.Load,
                    network.Reserve,
                    previousState.ToString(),
                    network.State.ToString(),
                    tick));
            }
        }
    }

    public bool ExpandCapacity(UtilityService service, int units, Money cost, long tick)
    {
        var networkEntity = FindNetwork(service);
        if (networkEntity == Entity.Null || units <= 0 || !world.Has<UtilityNetwork>(networkEntity))
            return false;
        if (cost.IsPositive && economy.Government.Balance < cost)
            return false;

        if (cost.IsPositive)
        {
            economy.Government.Balance -= cost;
            economy.Ledger.Record(
                Guid.NewGuid(), tick, Components.Finance.LedgerKind.UtilityPaid,
                EconomyAccountIds.Government, null, cost, "utility.capacity");
        }

        var network = world.Get<UtilityNetwork>(networkEntity);
        network.Capacity += units;
        network.Production += units;
        network.State = Classify(network);
        world.Get<UtilityNetwork>(networkEntity) = network;
        return true;
    }

    public bool FindLink(Entity building, UtilityService service, out Entity linkEntity, out BuildingServiceLink link)
    {
        var found = Entity.Null;
        var foundLink = default(BuildingServiceLink);
        world.Query(in new QueryDescription().WithAll<BuildingServiceLink, ServiceLinkOwner>(),
            (Entity entity, ref BuildingServiceLink candidate, ref ServiceLinkOwner owner) =>
            {
                if (found != Entity.Null || owner.Building != building || candidate.Service != service)
                    return;
                found = entity;
                foundLink = candidate;
            });
        linkEntity = found;
        link = foundLink;
        return found != Entity.Null;
    }

    public IEnumerable<(Entity Building, BuildingKind Kind, int WaterDemand, int ElectricityDemand)> Buildings()
    {
        var buildings = new Dictionary<Entity, BuildingKind>();
        world.Query(in new QueryDescription().WithAll<Placement>(),
            (Entity entity, ref Placement placement) => buildings[entity] = placement.Kind);

        // Properties and offices may be created without a Placement in tests
        // and legacy saves; they still need service links.
        world.Query(in new QueryDescription().WithAll<Property>().WithNone<Placement>(),
            (Entity entity, ref Property prop) => buildings[entity] = BuildingKind.Residential);
        world.Query(in new QueryDescription().WithAll<Office>().WithNone<Placement>(),
            (Entity entity, ref Office office) => buildings[entity] = BuildingKind.Office);

        foreach (var (entity, kind) in buildings.OrderBy(pair => pair.Key.Id))
        {
            var (water, electricity) = DemandFor(entity, kind);
            yield return (entity, kind, water, electricity);
        }
    }

    private (int Water, int Electricity) DemandFor(Entity building, BuildingKind kind)
    {
        switch (kind)
        {
            case BuildingKind.Residential:
            {
                var units = economy.Housing.UnitsByProperty.TryGetValue(building, out var list)
                    ? Math.Max(1, list.Count)
                    : 1;
                return (units * options.ResidentialWaterDemandPerUnit,
                        units * options.ResidentialElectricityDemandPerUnit);
            }
            case BuildingKind.Office:
            {
                var seats = world.Has<Office>(building)
                    ? Math.Max(1, world.Get<Office>(building).Capacity)
                    : 4;
                return (seats * options.OfficeWaterDemandPerSeat,
                        seats * options.OfficeElectricityDemandPerSeat);
            }
            case BuildingKind.School:
            case BuildingKind.Civic:
                return (options.SchoolDemand, options.SchoolDemand);
            case BuildingKind.Store:
                return (options.StoreDemand, options.StoreDemand);
            default:
                return (options.StoreDemand, options.StoreDemand);
        }
    }

    private void EnsureNetwork(UtilityService service, int production, int capacity)
    {
        if (FindNetwork(service) != Entity.Null)
            return;

        var entity = world.Create(new UtilityNetwork
        {
            Service = service,
            Production = production,
            Capacity = capacity,
            Load = 0,
            Reserve = capacity,
            State = UtilityNetworkState.Normal
        });
        var id = allocateNetworkId(entity);
        world.Add(entity, id);
    }

    private void EnsureLink(
        Entity building,
        Entity network,
        UtilityService service,
        int demand,
        int priority)
    {
        if (FindLink(building, service, out _, out _))
            return;

        var linkEntity = world.Create(
            new BuildingServiceLink
            {
                Network = network,
                Service = service,
                Demand = Math.Max(0, demand),
                Connected = true,
                Priority = priority
            },
            new ServiceLinkOwner { Building = building });
        var id = allocateNetworkId(linkEntity);
        world.Add(linkEntity, id);
    }

    private static int PriorityFor(BuildingKind kind) => kind switch
    {
        BuildingKind.Civic or BuildingKind.School => 1,
        BuildingKind.Residential => 2,
        BuildingKind.Office => 3,
        BuildingKind.Store => 4,
        _ => 4
    };

    private UtilityNetworkState Classify(in UtilityNetwork network)
    {
        if (network.Production <= 0 || network.Capacity <= 0)
            return UtilityNetworkState.Offline;
        var capacity = Math.Max(1, network.Capacity);
        var basis = network.Load * 10_000L / capacity;
        if (basis >= options.OfflineBasisPoints) return UtilityNetworkState.Offline;
        if (basis >= options.OverloadedBasisPoints) return UtilityNetworkState.Overloaded;
        if (basis >= options.StrainedBasisPoints) return UtilityNetworkState.Strained;
        return UtilityNetworkState.Normal;
    }

    private ulong GetNetworkId(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Values;

namespace Genelife.Components.Housing;

/// <summary>
/// Derived O(1) index of housing relationships. It mirrors the ECS components
/// and is written only in apply phases. Restore and tests rebuild it from the
/// world and verify it against component state.
/// </summary>
public sealed class HousingRegistry
{
    private readonly World world;
    private readonly HashSet<Entity> vacantUnits = new();
    private ulong nextLocalNetworkId = 1_000_000_000;

    public HousingRegistry(World world)
    {
        this.world = world;
        Market = new HousingMarketState();
    }

    public Dictionary<Entity, List<Entity>> UnitsByProperty { get; } = new();
    public Dictionary<Entity, Entity> PropertyByUnit { get; } = new();
    public Dictionary<Entity, Entity> HouseholdByUnit { get; } = new();
    public Dictionary<Entity, Entity> UnitByHousehold { get; } = new();
    public Dictionary<Entity, Entity> HouseholdBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> UnitsByOwner { get; } = new();
    public Dictionary<Entity, List<Entity>> MembersByHousehold { get; } = new();
    public Dictionary<Entity, Entity> OwnerByProperty { get; } = new();
    public Dictionary<Entity, NetworkId> NetworkIdByEntity { get; } = new();
    public Dictionary<NetworkId, Entity> EntityByNetworkId { get; } = new();

    public HousingMarketState Market { get; }
    public List<HousingSnapshot> MetricHistory { get; } = new();
    public Func<Entity, NetworkId>? NetworkIdAllocator { get; set; }

    public void AttachNetworkIdAllocator(Func<Entity, NetworkId> allocator)
    {
        NetworkIdAllocator = allocator;
    }

    public NetworkId AllocateNetworkId(Entity entity)
    {
        var id = NetworkIdAllocator?.Invoke(entity) ?? new NetworkId(nextLocalNetworkId++);
        AttachNetworkId(entity, id);
        return id;
    }

    public void AttachNetworkId(Entity entity, NetworkId networkId)
    {
        NetworkIdByEntity[entity] = networkId;
        EntityByNetworkId[networkId] = entity;
        if (world.IsAlive(entity) && !world.Has<NetworkId>(entity))
            world.Add(entity, networkId);
    }

    public void DetachNetworkId(Entity entity)
    {
        if (NetworkIdByEntity.Remove(entity, out var networkId))
            EntityByNetworkId.Remove(networkId);
    }

    public void AddProperty(Entity property, Entity owner)
    {
        if (owner == default)
            owner = Entity.Null;
        OwnerByProperty[property] = owner;
        if (!UnitsByProperty.ContainsKey(property))
            UnitsByProperty[property] = new List<Entity>();
        AddOwnerUnit(owner, property, isProperty: true);
    }

    public void SetPropertyOwner(Entity property, Entity owner)
    {
        if (OwnerByProperty.TryGetValue(property, out var previous))
            RemoveOwnerUnit(previous, property, isProperty: true);
        OwnerByProperty[property] = owner;
        AddOwnerUnit(owner, property, isProperty: true);

        if (UnitsByProperty.TryGetValue(property, out var units))
        {
            foreach (var unit in units)
            {
                if (!world.IsAlive(unit))
                    continue;
                var data = world.Get<HousingUnit>(unit);
                if (data.Owner == Entity.Null)
                    MoveUnitOwner(unit, previous, owner);
            }
        }
    }

    public void AddUnit(Entity unit, Entity property)
    {
        PropertyByUnit[unit] = property;
        if (!UnitsByProperty.TryGetValue(property, out var units))
        {
            units = new List<Entity>();
            UnitsByProperty[property] = units;
        }
        if (!units.Contains(unit))
            units.Add(unit);

        var data = world.IsAlive(unit) ? world.Get<HousingUnit>(unit) : default;
        var owner = EffectiveOwner(unit);
        AddOwnerUnit(owner, unit, isProperty: false);

        if (data.IsVacant)
            vacantUnits.Add(unit);

        if (world.IsAlive(property) && world.Has<Property>(property))
        {
            var propertyData = world.Get<Property>(property);
            Market.AddUnits(propertyData.District, 1);
        }
    }

    public void RemoveUnit(Entity unit)
    {
        if (HouseholdByUnit.TryGetValue(unit, out var household))
            MoveOut(unit, household);

        vacantUnits.Remove(unit);

        if (PropertyByUnit.TryGetValue(unit, out var property))
        {
            if (UnitsByProperty.TryGetValue(property, out var units))
                units.Remove(unit);
            if (world.IsAlive(property) && world.Has<Property>(property))
            {
                var propertyData = world.Get<Property>(property);
                Market.RemoveUnits(propertyData.District, 1);
            }
        }

        if (world.IsAlive(unit) && world.Has<HousingUnit>(unit))
            RemoveOwnerUnit(EffectiveOwner(unit), unit, isProperty: false);

        PropertyByUnit.Remove(unit);
    }

    public void AddMember(Entity sim, Entity household)
    {
        if (HouseholdBySim.TryGetValue(sim, out var existing) && existing != household)
            RemoveMember(sim);

        if (!MembersByHousehold.TryGetValue(household, out var members))
        {
            members = new List<Entity>();
            MembersByHousehold[household] = members;
        }
        if (!members.Contains(sim))
            members.Add(sim);

        HouseholdBySim[sim] = household;

        if (UnitByHousehold.TryGetValue(household, out var unit) && unit != Entity.Null)
        {
            SetResidence(sim, household, unit);
            RefreshOccupancy(household, unit);
        }
    }

    public void RemoveMember(Entity sim)
    {
        if (!HouseholdBySim.TryGetValue(sim, out var household))
            return;

        HouseholdBySim.Remove(sim);
        if (MembersByHousehold.TryGetValue(household, out var members))
            members.Remove(sim);

        if (world.IsAlive(sim) && world.Has<Residence>(sim))
            world.Remove<Residence>(sim);

        if (UnitByHousehold.TryGetValue(household, out var unit) && unit != Entity.Null)
            RefreshOccupancy(household, unit);
    }

    public void MoveIn(Entity unit, Entity household)
    {
        if (UnitByHousehold.TryGetValue(household, out var previousUnit) && previousUnit != unit)
            MoveOut(previousUnit, household);

        if (HouseholdByUnit.TryGetValue(unit, out var existing) && existing != household)
            MoveOut(unit, existing);

        UnitByHousehold[household] = unit;
        HouseholdByUnit[unit] = household;
        vacantUnits.Remove(unit);

        if (world.IsAlive(unit) && world.Has<HousingUnit>(unit))
        {
            var data = world.Get<HousingUnit>(unit);
            data.IsVacant = false;
            data.OccupantCount = MemberCount(household);
            world.Get<HousingUnit>(unit) = data;
        }

        if (world.IsAlive(household) && world.Has<Household>(household))
        {
            var householdData = world.Get<Household>(household);
            householdData.Unit = unit;
            householdData.MemberCount = MemberCount(household);
            world.Get<Household>(household) = householdData;
        }

        foreach (var member in GetMembers(household))
            SetResidence(member, household, unit);
    }

    public void MoveOut(Entity unit)
    {
        if (HouseholdByUnit.TryGetValue(unit, out var household))
            MoveOut(unit, household);
    }

    public void MoveOut(Entity unit, Entity household)
    {
        HouseholdByUnit.Remove(unit);
        if (UnitByHousehold.TryGetValue(household, out var mapped) && mapped == unit)
            UnitByHousehold.Remove(household);

        if (world.IsAlive(unit) && world.Has<HousingUnit>(unit))
        {
            var data = world.Get<HousingUnit>(unit);
            data.IsVacant = true;
            data.OccupantCount = 0;
            world.Get<HousingUnit>(unit) = data;
            vacantUnits.Add(unit);
        }

        if (world.IsAlive(household) && world.Has<Household>(household))
        {
            var householdData = world.Get<Household>(household);
            householdData.Unit = Entity.Null;
            householdData.MemberCount = MemberCount(household);
            world.Get<Household>(household) = householdData;
        }

        foreach (var member in GetMembers(household))
        {
            if (world.IsAlive(member) && world.Has<Residence>(member))
                world.Remove<Residence>(member);
        }
    }

    public void TransferUnit(Entity unit, Entity newOwner)
    {
        if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
            return;

        var data = world.Get<HousingUnit>(unit);
        var oldOwner = data.Owner;
        data.Owner = newOwner;
        world.Get<HousingUnit>(unit) = data;
        MoveUnitOwner(unit, oldOwner, newOwner);
    }

    public bool TryGetUnitForSim(Entity sim, out Entity unit)
    {
        unit = Entity.Null;
        if (!HouseholdBySim.TryGetValue(sim, out var household))
            return false;
        if (!UnitByHousehold.TryGetValue(household, out unit) || unit == Entity.Null)
            return false;
        return world.IsAlive(unit);
    }

    public Entity HouseholdOf(Entity sim) =>
        HouseholdBySim.TryGetValue(sim, out var household) ? household : Entity.Null;

    public Entity UnitOf(Entity household) =>
        UnitByHousehold.TryGetValue(household, out var unit) ? unit : Entity.Null;

    public Entity HouseholdIn(Entity unit) =>
        HouseholdByUnit.TryGetValue(unit, out var household) ? household : Entity.Null;

    public IReadOnlyList<Entity> GetMembers(Entity household) =>
        MembersByHousehold.TryGetValue(household, out var members) ? members : Array.Empty<Entity>();

    public int MemberCount(Entity household) =>
        MembersByHousehold.TryGetValue(household, out var members) ? members.Count : 0;

    public int VacancyCount(HousingTier? tier = null)
    {
        if (tier is null)
            return vacantUnits.Count;

        var count = 0;
        foreach (var unit in vacantUnits)
        {
            if (world.IsAlive(unit) && world.Has<HousingUnit>(unit) &&
                world.Get<HousingUnit>(unit).Tier == tier.Value)
                count++;
        }
        return count;
    }

    public IEnumerable<Entity> VacantUnits(HousingTier? tier = null)
    {
        foreach (var unit in vacantUnits)
        {
            if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
                continue;
            if (tier is not null && world.Get<HousingUnit>(unit).Tier != tier.Value)
                continue;
            yield return unit;
        }
    }

    public Money RentRoll(Entity owner)
    {
        var total = Money.Zero;
        if (!UnitsByOwner.TryGetValue(owner, out var units))
            return total;

        foreach (var unit in units)
        {
            if (!HouseholdByUnit.TryGetValue(unit, out var household))
                continue;
            if (!world.IsAlive(household) || !world.Has<Household>(household))
                continue;
            total += world.Get<Household>(household).MonthlyRent;
        }
        return total;
    }

    public Money TotalRentRoll()
    {
        var total = Money.Zero;
        foreach (var household in HouseholdByUnit.Values)
        {
            if (world.IsAlive(household) && world.Has<Household>(household))
                total += world.Get<Household>(household).MonthlyRent;
        }
        return total;
    }

    public void Rebuild(World currentWorld)
    {
        Clear();

        var staleResidences = new List<Entity>();
        currentWorld.Query(in new QueryDescription().WithAll<Residence>(),
            (Entity entity, ref Residence _) => staleResidences.Add(entity));
        foreach (var entity in staleResidences)
        {
            if (currentWorld.IsAlive(entity) && currentWorld.Has<Residence>(entity))
                currentWorld.Remove<Residence>(entity);
        }

        var propertyQuery = new QueryDescription().WithAll<Property>();
        currentWorld.Query(in propertyQuery, (Entity entity, ref Property property) =>
        {
            if (currentWorld.Has<NetworkId>(entity))
                AttachNetworkId(entity, currentWorld.Get<NetworkId>(entity));
            AddProperty(entity, property.Owner);
        });

        var unitQuery = new QueryDescription().WithAll<HousingUnit>();
        currentWorld.Query(in unitQuery, (Entity entity, ref HousingUnit unit) =>
        {
            if (currentWorld.Has<NetworkId>(entity))
                AttachNetworkId(entity, currentWorld.Get<NetworkId>(entity));
            AddUnit(entity, unit.Property);
        });

        var householdQuery = new QueryDescription().WithAll<Household>();
        currentWorld.Query(in householdQuery, (Entity entity, ref Household _) =>
        {
            if (currentWorld.Has<NetworkId>(entity))
                AttachNetworkId(entity, currentWorld.Get<NetworkId>(entity));
        });

        var memberQuery = new QueryDescription().WithAll<HouseholdMember>();
        currentWorld.Query(in memberQuery, (Entity entity, ref HouseholdMember member) =>
        {
            if (currentWorld.IsAlive(member.Household))
                AddMember(entity, member.Household);
        });

        var occupied = new List<(Entity Household, Entity Unit)>();
        currentWorld.Query(in householdQuery, (Entity entity, ref Household household) =>
        {
            if (household.Unit != Entity.Null)
                occupied.Add((entity, household.Unit));
        });

        foreach (var (household, unit) in occupied)
            MoveIn(unit, household);

        RefreshVacancy();

        foreach (var (property, units) in UnitsByProperty)
        {
            if (!currentWorld.IsAlive(property) || !currentWorld.Has<Property>(property))
                continue;
            var data = currentWorld.Get<Property>(property);
            data.UnitCount = units.Count;
            currentWorld.Get<Property>(property) = data;
        }
    }

    public void RefreshVacancy()
    {
        vacantUnits.Clear();
        var query = new QueryDescription().WithAll<HousingUnit>();
        world.Query(in query, (Entity entity, ref HousingUnit unit) =>
        {
            var vacant = !HouseholdByUnit.ContainsKey(entity);
            if (unit.IsVacant != vacant)
            {
                unit.IsVacant = vacant;
                world.Get<HousingUnit>(entity) = unit;
            }
            if (vacant)
                vacantUnits.Add(entity);
        });
    }

    public List<string> Verify()
    {
        var issues = new List<string>();

        foreach (var (unit, household) in HouseholdByUnit)
        {
            if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
                issues.Add($"unit {unit.Id} is missing its HousingUnit component");
            if (!world.IsAlive(household) || !world.Has<Household>(household))
                issues.Add($"household {household.Id} on unit {unit.Id} is missing its Household component");
            else if (UnitByHousehold.GetValueOrDefault(household) != unit)
                issues.Add($"household {household.Id} and unit {unit.Id} disagree about occupancy");
        }

        foreach (var (household, unit) in UnitByHousehold)
        {
            if (HouseholdByUnit.GetValueOrDefault(unit) != household)
                issues.Add($"unit {unit.Id} and household {household.Id} disagree about occupancy");
        }

        foreach (var (sim, household) in HouseholdBySim)
        {
            if (!world.IsAlive(sim) || !world.Has<HouseholdMember>(sim))
                issues.Add($"sim {sim.Id} is mapped to a household without membership");
            else if (world.Get<HouseholdMember>(sim).Household != household)
                issues.Add($"sim {sim.Id} membership disagrees with the registry");
        }

        foreach (var (household, members) in MembersByHousehold)
        {
            foreach (var member in members)
            {
                if (HouseholdBySim.GetValueOrDefault(member) != household)
                    issues.Add($"member {member.Id} is not registered in household {household.Id}");
            }
        }

        foreach (var unit in vacantUnits)
        {
            if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
                issues.Add($"vacant unit {unit.Id} no longer exists");
            else if (HouseholdByUnit.ContainsKey(unit))
                issues.Add($"unit {unit.Id} is vacant but occupied");
        }

        return issues;
    }

    public void Clear()
    {
        UnitsByProperty.Clear();
        PropertyByUnit.Clear();
        HouseholdByUnit.Clear();
        UnitByHousehold.Clear();
        HouseholdBySim.Clear();
        UnitsByOwner.Clear();
        MembersByHousehold.Clear();
        OwnerByProperty.Clear();
        NetworkIdByEntity.Clear();
        EntityByNetworkId.Clear();
        vacantUnits.Clear();
        Market.Clear();
        MetricHistory.Clear();
    }

    private void SetResidence(Entity sim, Entity household, Entity unit)
    {
        if (!world.IsAlive(sim))
            return;

        var residence = new Residence(household, unit);
        if (world.Has<Residence>(sim))
            world.Get<Residence>(sim) = residence;
        else
            world.Add(sim, residence);
    }

    private void RefreshOccupancy(Entity household, Entity unit)
    {
        var count = MemberCount(household);
        if (world.IsAlive(unit) && world.Has<HousingUnit>(unit))
        {
            var data = world.Get<HousingUnit>(unit);
            data.OccupantCount = count;
            world.Get<HousingUnit>(unit) = data;
        }

        if (world.IsAlive(household) && world.Has<Household>(household))
        {
            var data = world.Get<Household>(household);
            data.MemberCount = count;
            world.Get<Household>(household) = data;
        }
    }

    private void AddOwnerUnit(Entity owner, Entity entity, bool isProperty)
    {
        if (!UnitsByOwner.TryGetValue(owner, out var list))
        {
            list = new List<Entity>();
            UnitsByOwner[owner] = list;
        }
        if (!list.Contains(entity))
            list.Add(entity);
    }

    private void RemoveOwnerUnit(Entity owner, Entity entity, bool isProperty)
    {
        if (UnitsByOwner.TryGetValue(owner, out var list))
            list.Remove(entity);
    }

    private void MoveUnitOwner(Entity unit, Entity oldOwner, Entity newOwner)
    {
        RemoveOwnerUnit(oldOwner, unit, isProperty: false);
        AddOwnerUnit(newOwner, unit, isProperty: false);
    }

    public Entity EffectiveOwner(Entity unit)
    {
        if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
            return Entity.Null;

        var data = world.Get<HousingUnit>(unit);
        if (data.Owner != Entity.Null)
            return data.Owner;
        return OwnerByProperty.GetValueOrDefault(data.Property);
    }
}

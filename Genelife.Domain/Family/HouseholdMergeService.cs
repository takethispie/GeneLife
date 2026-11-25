using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;

namespace Genelife.Family;

/// <summary>
/// Applies household merge/split through the housing registry so capacity,
/// occupancy and residence caches stay consistent. It never moves money.
/// </summary>
public sealed class HouseholdMergeService
{
    private readonly World world;
    private readonly EconomyContext economy;

    public HouseholdMergeService(World world, EconomyContext economy)
    {
        this.world = world;
        this.economy = economy;
    }

    public bool TryMergePartners(
        Entity first,
        Entity second,
        out Entity fromHousehold,
        out Entity toHousehold,
        out ulong fromNetworkId,
        out ulong toNetworkId)
    {
        fromHousehold = Entity.Null;
        toHousehold = Entity.Null;
        fromNetworkId = 0;
        toNetworkId = 0;

        var registry = economy.Housing;
        var firstHousehold = registry.HouseholdOf(first);
        var secondHousehold = registry.HouseholdOf(second);
        if (firstHousehold == Entity.Null || secondHousehold == Entity.Null)
            return false;
        if (firstHousehold == secondHousehold)
        {
            fromHousehold = firstHousehold;
            toHousehold = secondHousehold;
            fromNetworkId = NetworkIdOf(firstHousehold);
            toNetworkId = fromNetworkId;
            return true;
        }

        var target = ChooseTarget(firstHousehold, secondHousehold);
        var source = target == firstHousehold ? secondHousehold : firstHousehold;

        if (!CanMerge(source, target))
            return false;

        fromHousehold = source;
        toHousehold = target;
        fromNetworkId = NetworkIdOf(source);
        toNetworkId = NetworkIdOf(target);

        var sourceUnit = registry.UnitOf(source);
        var sourceMembers = registry.GetMembers(source).ToList();
        var targetHead = world.Has<Household>(target) ? world.Get<Household>(target).Head : Entity.Null;

        foreach (var member in sourceMembers)
        {
            if (!world.IsAlive(member))
                continue;

            var oldRole = world.Has<HouseholdMember>(member)
                ? world.Get<HouseholdMember>(member).Role
                : HouseholdRole.Other;
            var role = RoleFor(member, first, second, oldRole);
            registry.RemoveMember(member);
            SetRole(member, target, role);
            registry.AddMember(member, target);
        }

        if (sourceUnit != Entity.Null)
            registry.MoveOut(sourceUnit, source);

        registry.DetachNetworkId(source);
        if (world.IsAlive(source))
            world.Destroy(source);

        if (targetHead != Entity.Null && world.IsAlive(target) && world.Has<Household>(target))
        {
            var data = world.Get<Household>(target);
            data.Head = targetHead;
            data.MemberCount = registry.MemberCount(target);
            world.Get<Household>(target) = data;
        }

        return true;
    }

    public bool TrySplit(Entity genos, long tick, out Entity household)
    {
        household = Entity.Null;
        var registry = economy.Housing;
        var existing = registry.HouseholdOf(genos);
        if (existing == Entity.Null)
            return false;
        if (world.Has<Household>(existing) && world.Get<Household>(existing).Head == genos &&
            registry.MemberCount(existing) > 1)
            return false;

        household = world.Create(new Household
        {
            Head = genos,
            Unit = Entity.Null,
            Tenure = Tenure.Renter,
            MoveInTick = tick,
            MemberCount = 1
        });
        registry.AllocateNetworkId(household);
        registry.RemoveMember(genos);
        SetRole(genos, household, HouseholdRole.Head);
        registry.AddMember(genos, household);

        if (world.IsAlive(existing) && registry.MemberCount(existing) == 0)
        {
            var unit = registry.UnitOf(existing);
            if (unit != Entity.Null)
                registry.MoveOut(unit, existing);
            registry.DetachNetworkId(existing);
            world.Destroy(existing);
        }

        new HousingAllocator(world, economy).TryAssignBest(household, tick);
        return true;
    }

    private Entity ChooseTarget(Entity first, Entity second)
    {
        var firstUnit = economy.Housing.UnitOf(first);
        var secondUnit = economy.Housing.UnitOf(second);
        if (firstUnit == Entity.Null && secondUnit != Entity.Null)
            return second;
        if (secondUnit == Entity.Null && firstUnit != Entity.Null)
            return first;

        var firstOwned = world.Has<Household>(first) && world.Get<Household>(first).Tenure == Tenure.Owner;
        var secondOwned = world.Has<Household>(second) && world.Get<Household>(second).Tenure == Tenure.Owner;
        if (firstOwned != secondOwned)
            return firstOwned ? first : second;

        var firstMembers = economy.Housing.MemberCount(first);
        var secondMembers = economy.Housing.MemberCount(second);
        if (firstMembers != secondMembers)
            return firstMembers > secondMembers ? first : second;

        return NetworkIdOf(first) <= NetworkIdOf(second) ? first : second;
    }

    private bool CanMerge(Entity source, Entity target)
    {
        var registry = economy.Housing;
        var targetUnit = registry.UnitOf(target);
        if (targetUnit == Entity.Null || !world.IsAlive(targetUnit) || !world.Has<HousingUnit>(targetUnit))
            return false;
        if (world.Has<Household>(source) && world.Get<Household>(source).Tenure == Tenure.Owner)
            return false;
        if (registry.MemberCount(source) > 1)
            return false;

        var capacity = world.Get<HousingUnit>(targetUnit).Capacity;
        return registry.MemberCount(target) + registry.MemberCount(source) <= capacity;
    }

    private static HouseholdRole RoleFor(
        Entity member,
        Entity first,
        Entity second,
        HouseholdRole oldRole)
    {
        if (member == first || member == second)
            return HouseholdRole.Partner;
        return oldRole;
    }

    private void SetRole(Entity genos, Entity household, HouseholdRole role)
    {
        var membership = new HouseholdMember(household, role);
        if (world.Has<HouseholdMember>(genos))
            world.Get<HouseholdMember>(genos) = membership;
        else
            world.Add(genos, membership);
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}

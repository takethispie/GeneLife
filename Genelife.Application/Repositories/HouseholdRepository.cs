using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Services;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Api.Repositories;

/// <summary>
/// Household lifecycle: create, join, leave, head change and dissolve. Every
/// mutation updates the housing registry and the derived Residence component.
/// </summary>
public sealed class HouseholdRepository
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext economy;

    public HouseholdRepository(World world, NetworkIdRegistry networkIds, EconomyContext economy)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
    }

    public NetworkId Create(ulong headGenosId, long tick = 0, Tenure tenure = Tenure.Renter)
    {
        if (!networkIds.TryGetEntity(new NetworkId(headGenosId), out var head) ||
            !world.IsAlive(head) ||
            !world.Has<GenName>(head))
            throw new InvalidOperationException("Head genos not found.");

        if (!IsAdult(head))
            throw new InvalidOperationException("A household head must be an adult.");
        if (economy.Housing.HouseholdByGenos.ContainsKey(head))
            throw new InvalidOperationException("The genos already belongs to a household.");
        if (world.Has<HouseholdMember>(head))
            throw new InvalidOperationException("The genos already has a household membership.");

        var entity = world.Create(new Household
        {
            Head = head,
            Unit = Entity.Null,
            Tenure = tenure,
            MoveInTick = tick,
            MemberCount = 1
        });
        var id = networkIds.Allocate(entity);
        world.Add(entity, id);

        world.Add(head, new HouseholdMember(entity, HouseholdRole.Head));
        economy.Housing.AddMember(head, entity);

        economy.Emit(new HouseholdFormed(id.Value, headGenosId, tick));
        return id;
    }

    public bool AddMember(NetworkId householdId, ulong genosId, HouseholdRole role, long tick = 0)
    {
        if (!TryGet(householdId, out var household))
            return false;
        if (!networkIds.TryGetEntity(new NetworkId(genosId), out var genos) ||
            !world.IsAlive(genos) ||
            !world.Has<GenName>(genos))
            throw new InvalidOperationException("Genos not found.");
        if (economy.Housing.HouseholdByGenos.TryGetValue(genos, out var existing))
        {
            if (existing == household)
                return true;
            throw new InvalidOperationException("The genos already belongs to a different household.");
        }

        if (role == HouseholdRole.Head && world.Get<Household>(household).Head != Entity.Null)
            throw new InvalidOperationException("The household already has a head.");

        if (!IsAdult(genos) && role is HouseholdRole.Head or HouseholdRole.Partner)
            throw new InvalidOperationException("A child cannot be the household head or partner.");

        world.Add(genos, new HouseholdMember(household, role));
        economy.Housing.AddMember(genos, household);

        ref var data = ref world.Get<Household>(household);
        if (role == HouseholdRole.Head || data.Head == Entity.Null)
            data.Head = genos;
        data.MemberCount = economy.Housing.MemberCount(household);

        economy.Emit(new HouseholdMemberJoined(householdId.Value, genosId, role.ToString(), tick));
        return true;
    }

    public bool RemoveMember(NetworkId householdId, ulong genosId, long tick = 0)
    {
        if (!TryGet(householdId, out var household))
            return false;
        if (!networkIds.TryGetEntity(new NetworkId(genosId), out var genos) || !world.IsAlive(genos))
            return false;
        if (!world.Has<HouseholdMember>(genos) || world.Get<HouseholdMember>(genos).Household != household)
            return false;

        world.Remove<HouseholdMember>(genos);
        economy.Housing.RemoveMember(genos);
        economy.Emit(new HouseholdMemberLeft(householdId.Value, genosId, tick));

        var members = economy.Housing.GetMembers(household).ToList();
        if (members.Count == 0)
        {
            Dissolve(household, tick);
            return true;
        }

        ref var data = ref world.Get<Household>(household);
        data.MemberCount = members.Count;
        if (data.Head == genos)
        {
            var newHead = SelectHead(household, members);
            data.Head = newHead;
            if (newHead != Entity.Null && world.Has<HouseholdMember>(newHead))
                world.Get<HouseholdMember>(newHead).Role = HouseholdRole.Head;
            economy.Emit(new HouseholdHeadChanged(
                householdId.Value,
                newHead == Entity.Null ? 0 : NetworkIdOf(newHead),
                tick));
        }
        return true;
    }

    public bool Dissolve(NetworkId householdId, long tick = 0)
    {
        if (!TryGet(householdId, out var household))
            return false;
        Dissolve(household, tick);
        return true;
    }

    public bool TryGet(NetworkId householdId, out Entity entity)
    {
        if (networkIds.TryGetEntity(householdId, out entity) &&
            world.IsAlive(entity) &&
            world.Has<Household>(entity))
            return true;
        entity = Entity.Null;
        return false;
    }

    public Entity Head(Entity household) =>
        world.IsAlive(household) && world.Has<Household>(household)
            ? world.Get<Household>(household).Head
            : Entity.Null;

    private void Dissolve(Entity household, long tick)
    {
        var unit = economy.Housing.UnitOf(household);
        if (unit != Entity.Null)
            economy.Housing.MoveOut(unit, household);

        foreach (var member in economy.Housing.GetMembers(household).ToList())
        {
            if (!world.IsAlive(member))
                continue;
            if (world.Has<HouseholdMember>(member))
                world.Remove<HouseholdMember>(member);
            economy.Housing.RemoveMember(member);
        }

        networkIds.TryGetNetworkId(household, out var networkId);
        networkIds.Remove(household);
        economy.Housing.DetachNetworkId(household);
        if (networkId.Value != 0)
            economy.Emit(new HouseholdDissolved(networkId.Value, tick));

        if (world.IsAlive(household))
            world.Destroy(household);
    }

    private Entity SelectHead(Entity household, List<Entity> members)
    {
        Entity best = Entity.Null;
        var bestSalary = -1L;
        foreach (var member in members)
        {
            if (!world.IsAlive(member) || !IsAdult(member))
                continue;

            var salary = world.Has<Employee>(member)
                ? world.Get<Employee>(member).SalaryPerCycle.Cents
                : 0L;
            var networkId = NetworkIdOf(member);
            if (salary > bestSalary ||
                (salary == bestSalary &&
                 (best == Entity.Null || networkId < NetworkIdOf(best))))
            {
                best = member;
                bestSalary = salary;
            }
        }
        return best;
    }

    private bool IsAdult(Entity genos) =>
        !world.Has<Alive>(genos) || world.Get<Alive>(genos).Age >= 18;

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}

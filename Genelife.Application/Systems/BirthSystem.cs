using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Application.Systems;
using Genelife.Values;

using Genelife.Family;
namespace Genelife.Application.Systems;

/// <summary>
/// Completes due pregnancies. A birth is never blocked by housing capacity; the
/// newborn always joins the guardian household and may overcrowd the unit.
/// </summary>
public sealed class BirthSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly PregnancyIndex pregnancies;
    private readonly PregnancyService pregnanciesService;
    private readonly NewbornFactory factory;
    private readonly Func<Entity, NetworkId> allocateNetworkId;
    private readonly ReproductionOptions options;

    private readonly int cadenceOffset;

    public BirthSystem(
        World world,
        EconomyContext economy,
        PregnancyIndex pregnancies,
        PregnancyService pregnancyService,
        NewbornFactory factory,
        Func<Entity, NetworkId> allocateNetworkId,
        ReproductionOptions options,
        int cadenceOffset = 0)
    {
        this.world = world;
        this.economy = economy;
        this.pregnancies = pregnancies;
        pregnanciesService = pregnancyService;
        this.factory = factory;
        this.allocateNetworkId = allocateNetworkId;
        this.options = options;
        this.cadenceOffset = cadenceOffset;
    }

    public string Name => "Birth";
    public int Order => 380;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth), cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        options.Validate();

        var due = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<Pregnancy>(),
            (Entity entity, ref Pregnancy pregnancy) =>
            {
                if (pregnancy.Status is PregnancyStatus.Active or PregnancyStatus.ReadyToBirth &&
                    tick >= pregnancy.DueTick)
                    due.Add(entity);
            });
        due.Sort((a, b) => NetworkIdOf(a).CompareTo(NetworkIdOf(b)));

        foreach (var pregnancy in due)
        {
            if (!world.IsAlive(pregnancy) || !pregnanciesService.TryRead(pregnancy, out var data))
                continue;
            if (data.Status is PregnancyStatus.Completed or PregnancyStatus.Lost or PregnancyStatus.Cancelled)
                continue;

            var carrying = world.IsAlive(data.CarryingParent) && !world.Has<Dead>(data.CarryingParent)
                ? data.CarryingParent
                : Entity.Null;
            var other = world.IsAlive(data.OtherParent) && !world.Has<Dead>(data.OtherParent)
                ? data.OtherParent
                : Entity.Null;

            if (carrying == Entity.Null && other == Entity.Null)
            {
                pregnanciesService.Cancel(pregnancy, tick);
                economy.Emit(new PregnancyLost(NetworkIdOf(pregnancy), "parents-unavailable", tick));
                continue;
            }

            var parentA = carrying != Entity.Null ? carrying : other;
            var parentB = other != Entity.Null && other != parentA ? other : Entity.Null;
            var household = GuardianHousehold(parentA, parentB, tick);
            var children = Math.Max(1, data.ChildrenExpected);
            for (var i = 0; i < children; i++)
            {
                var newborn = factory.Create(parentA, parentB, household, pregnancy, tick);
                economy.Life.Index.OnBorn(LifeStage.Education);
                economy.Life.BirthsThisYear++;
                economy.Emit(new ChildBorn(
                    NetworkIdOf(newborn.Entity),
                    NetworkIdOf(parentA),
                    NetworkIdOf(parentB),
                    NetworkIdOf(household),
                    tick));
                economy.Emit(new BirthRecorded(
                    NetworkIdOf(newborn.Entity),
                    NetworkIdOf(pregnancy),
                    newborn.Name,
                    tick));
            }

            ChargeDelivery(household, tick);
            pregnanciesService.Complete(pregnancy, tick);
        }
    }

    private Entity GuardianHousehold(Entity firstParent, Entity secondParent, long tick)
    {
        var household = Parenting.HouseholdOf(world, economy, firstParent);
        if (household != Entity.Null)
            return household;
        household = Parenting.HouseholdOf(world, economy, secondParent);
        if (household != Entity.Null)
            return household;

        var parent = firstParent != Entity.Null ? firstParent : secondParent;
        if (parent == Entity.Null)
            return Entity.Null;

        household = world.Create(new Household
        {
            Head = parent,
            Tenure = Tenure.Renter,
            MemberCount = 1,
            MoveInTick = tick
        });
        world.Add(household, allocateNetworkId(household));
        world.Add(parent, new HouseholdMember(household, HouseholdRole.Head));
        economy.Housing.AddMember(parent, household);
        new HousingAllocator(world, economy).TryAssignBest(household, tick);
        return household;
    }

    private void ChargeDelivery(Entity household, long tick)
    {
        var cost = options.DeliveryCost;
        if (!cost.IsPositive || household == Entity.Null)
            return;

        var finance = new HouseholdFinanceService(world, economy);
        finance.TryDebit(
            household,
            cost,
            LedgerKind.MedicalCare,
            tick,
            EconomyAccountIds.Government,
            "birth.delivery",
            out var shortfall);
        var paid = cost - shortfall;
        if (paid.IsPositive)
            economy.Government.Balance += paid;
    }

    private ulong NetworkIdOf(Entity entity) =>
        entity != Entity.Null && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0;
}

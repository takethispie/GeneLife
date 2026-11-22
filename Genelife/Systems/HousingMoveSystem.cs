using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Voluntary moves and homeless recovery. A household responds to rent burden,
/// overcrowding, poor condition or a better neighbourhood, pays a deposit plus
/// moving fee and respects a three-month cooldown.
/// </summary>
public class HousingMoveSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly HouseholdFinanceService finance;
    private readonly HousingAllocator allocator;
    private readonly int interval;

    public HousingMoveSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
        finance = new HouseholdFinanceService(world, economy);
        allocator = new HousingAllocator(world, economy);
        this.interval = interval ?? Math.Max(1, economy.Clock.TicksPerMonth);
    }

    public string Name => "HousingMove";
    public int Order => 512;
    public Cadence Cadence => new(interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var households = new List<(Entity Entity, Household Household)>();
        var query = new QueryDescription().WithAll<Household>();
        world.Query(in query, (Entity entity, ref Household household) => households.Add((entity, household)));
        households.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

        var cooldown = economy.Clock.TicksPerMonth * economy.HousingOptions.MoveCooldownMonths;

        foreach (var (entity, household) in households)
        {
            if (!world.IsAlive(entity))
                continue;

            var data = world.Get<Household>(entity);
            if (data.Tenure == Tenure.Owner)
                continue;

            if (data.Unit == Entity.Null)
            {
                if (allocator.TryAssignBest(entity, tick))
                    registry.Market.MovesThisYear++;
                continue;
            }

            if (tick - data.LastMoveTick < cooldown)
                continue;

            if (!world.IsAlive(data.Unit) || !world.Has<HousingUnit>(data.Unit))
                continue;

            var unit = world.Get<HousingUnit>(data.Unit);
            var income = finance.MonthlyIncome(entity);
            var burden = income.IsPositive
                ? data.MonthlyRent.Cents / (decimal)income.Cents
                : 0m;

            if (burden > economy.HousingOptions.RentBurdenTrigger)
                data.OverburdenMonths++;
            else
                data.OverburdenMonths = 0;

            var overburdened = data.OverburdenMonths >= economy.HousingOptions.OverburdenMonthsBeforeMove;
            var overcrowded = unit.OccupantCount > unit.Capacity;
            var poorCondition = unit.Condition < 0.3f;

            var random = economy.Random.For($"housing.move.{tick}");
            var upgrade = income.IsPositive &&
                data.MonthlyRent.Cents * 3 < income.Cents &&
                random.NextDouble() < 0.02;

            if (!overburdened && !overcrowded && !poorCondition && !upgrade)
            {
                world.Get<Household>(entity) = data;
                continue;
            }

            if (data.NoticeStage >= 3 && data.RentArrears.IsPositive)
            {
                world.Get<Household>(entity) = data;
                continue;
            }

            var members = Math.Max(1, registry.MemberCount(entity));
            var maxTier = HousingTierPolicy.Next(unit.Tier);
            var candidates = allocator.RankVacant(
                income,
                members,
                includeShelter: false,
                requireAffordable: true,
                maxTier: maxTier);

            UnitCandidate? target = null;
            foreach (var candidate in candidates)
            {
                if (candidate.Unit == data.Unit)
                    continue;
                if (target is null)
                {
                    target = candidate;
                    continue;
                }

                var current = target.Value;
                if (candidate.Neighbourhood > current.Neighbourhood ||
                    (Math.Abs(candidate.Neighbourhood - current.Neighbourhood) < 0.001f &&
                     candidate.Rent < current.Rent))
                    target = candidate;
            }

            if (target is null)
            {
                world.Get<Household>(entity) = data;
                continue;
            }

            var deposit = HousingCostPolicy.Deposit(target.Value.Rent);
            var fee = HousingCostPolicy.MovingFee;
            if (finance.TotalWealth(entity) < deposit + fee)
            {
                world.Get<Household>(entity) = data;
                continue;
            }

            var landlord = registry.EffectiveOwner(target.Value.Unit);
            var landlordNetworkId = landlord == Entity.Null
                ? EconomyAccountIds.Government
                : NetworkIdOf(landlord);
            finance.TryDebit(
                entity,
                deposit,
                LedgerKind.RentDeposit,
                tick,
                landlordNetworkId,
                "housing.deposit",
                out var depositShortfall);
            finance.TryDebit(
                entity,
                fee,
                LedgerKind.MovingFee,
                tick,
                EconomyAccountIds.Government,
                "housing.moving",
                out var feeShortfall);

            if (depositShortfall.IsPositive || feeShortfall.IsPositive)
            {
                world.Get<Household>(entity) = data;
                continue;
            }

            CreditOwner(landlord, deposit, tick);
            economy.Government.Balance += fee;

            var oldUnit = data.Unit;
            registry.MoveOut(oldUnit, entity);

            if (!allocator.TryAssign(
                    entity,
                    target.Value.Unit,
                    Tenure.Renter,
                    tick,
                    target.Value.Rent,
                    "housing.move",
                    resetArrears: false))
                continue;

            data = world.Get<Household>(entity);
            data.LastMoveTick = tick;
            data.OverburdenMonths = 0;
            world.Get<Household>(entity) = data;
            registry.Market.MovesThisYear++;

            economy.Emit(new HouseholdMoved(
                NetworkIdOf(entity),
                NetworkIdOf(oldUnit),
                NetworkIdOf(target.Value.Unit),
                deposit.Cents,
                fee.Cents,
                tick));
        }
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;

    private void CreditOwner(Entity owner, Money amount, long tick)
    {
        if (!amount.IsPositive)
            return;

        if (owner == Entity.Null)
        {
            economy.Government.Balance += amount;
            return;
        }

        if (world.IsAlive(owner) && world.Has<Genelife.Components.Company.CompanyName>(owner))
        {
            economy.Accounts.ApplySettlement(owner, amount, tick, revenue: true);
            return;
        }

        if (world.IsAlive(owner) && world.Has<Household>(owner))
        {
            var head = finance.Head(owner);
            if (head != Entity.Null && world.IsAlive(head))
            {
                if (!world.Has<Wallet>(head))
                    world.Add(head, new Wallet(Money.Zero));
                world.Get<Wallet>(head).Balance += amount;
            }
            return;
        }

        economy.Government.Balance += amount;
    }
}

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Monthly needs-driven consumer spending. Cash is debited only when a company
/// can supply the category; otherwise unmet demand is recorded and no money is
/// destroyed. Food and hygiene are protected before discretionary categories.
/// </summary>
public class ConsumerSpendingSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription simQuery;
    private readonly int interval;

    public ConsumerSpendingSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
        simQuery = new QueryDescription().WithAll<GenName, Wallet>();
    }

    public string Name => "ConsumerSpending";
    public int Order => 460;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var suppliers = CollectSuppliers();
        if (suppliers.Count == 0)
        {
            RecordUnmetForEveryone(tick);
            return;
        }

        var random = economy.Random.For("revenue.attribution");
        var spenders = new List<Entity>();
        world.Query(in simQuery, (Entity entity, ref GenName _, ref Wallet _) => spenders.Add(entity));

        var totalSpent = Money.Zero;
        var totalUnmet = Money.Zero;
        var households = 0;
        foreach (var entity in spenders)
        {
            if (!world.IsAlive(entity))
                continue;

            var (spent, unmet) = Spend(tick, entity, suppliers, random);
            totalSpent += spent;
            totalUnmet += unmet;
            households++;
        }

        if (households > 0)
        {
            economy.Emit(new ConsumerSpendingBatchCompleted(
                households,
                totalSpent.Cents,
                totalUnmet.Cents,
                tick));
        }
    }

    private (Money Spent, Money Unmet) Spend(long tick, Entity sim, List<Supplier> suppliers, Random random)
    {
        ref var wallet = ref world.Get<Wallet>(sim);
        var state = world.Has<ConsumptionState>(sim)
            ? world.Get<ConsumptionState>(sim)
            : new ConsumptionState();

        state.StartMonth();

        var options = economy.Options;
        var budget = options.MonthlyConsumptionBudget;
        var available = wallet.Balance.Min(budget);
        var spent = Money.Zero;
        var unmet = Money.Zero;

        var requiredTarget = budget.Multiply(options.FoodShare + options.HygieneShare);
        var requiredBudget = available.Min(requiredTarget);

        // Food first, then hygiene. The remainder is protected before leisure.
        var foodBudget = requiredBudget.Min(budget.Multiply(options.FoodShare));
        var hygieneBudget = (requiredBudget - foodBudget).Min(budget.Multiply(options.HygieneShare));

        spent += Buy("Food", foodBudget, sim, suppliers, random, ref state, ref unmet, tick);
        spent += Buy("Hygiene", hygieneBudget, sim, suppliers, random, ref state, ref unmet, tick);

        var remaining = available - requiredBudget;
        if (remaining.IsPositive)
        {
            var leisureBudget = remaining.Min(budget.Multiply(options.LeisureShare));
            spent += Buy("Leisure", leisureBudget, sim, suppliers, random, ref state, ref unmet, tick);
            remaining -= leisureBudget;

            var healthBudget = remaining.Min(budget.Multiply(options.HealthShare));
            spent += Buy("Health", healthBudget, sim, suppliers, random, ref state, ref unmet, tick);
            remaining -= healthBudget;

            var durableBudget = remaining;
            spent += Buy("Durable", durableBudget, sim, suppliers, random, ref state, ref unmet, tick);
        }

        if (available < budget)
            unmet += budget - available;

        economy.ConsumptionThisYear += spent;

        state.LastCycleSpent = spent;
        state.LifetimeSpent += spent;
        state.UnmetDemand += unmet;
        state.LastSpendTick = tick;

        if (world.Has<ConsumptionState>(sim))
            world.Get<ConsumptionState>(sim) = state;
        else
            world.Add(sim, state);

        return (spent, unmet);
    }

    private Money Buy(
        string category,
        Money budget,
        Entity sim,
        List<Supplier> suppliers,
        Random random,
        ref ConsumptionState state,
        ref Money unmet,
        long tick)
    {
        if (!budget.IsPositive)
            return Money.Zero;

        var supplier = ChooseSupplier(suppliers, random);
        if (supplier is null)
        {
            unmet += budget;
            return Money.Zero;
        }

        ref var wallet = ref world.Get<Wallet>(sim);
        var amount = budget.Min(wallet.Balance);
        if (!amount.IsPositive)
            return Money.Zero;

        wallet.Balance -= amount;
        economy.BookRevenue(
            supplier.Company,
            category,
            CompanyAccountService.NetworkIdOf(world, sim),
            amount,
            tick);

        switch (category)
        {
            case "Food": state.FoodPurchased = true; break;
            case "Hygiene": state.HygienePurchased = true; break;
            case "Leisure": state.LeisurePurchased = true; break;
            case "Health": state.HealthPurchased = true; break;
            case "Durable": state.DurablePurchased = true; break;
        }

        return amount;
    }

    private void RecordUnmetForEveryone(long tick)
    {
        var budget = economy.Options.MonthlyConsumptionBudget;
        var sims = new List<Entity>();
        world.Query(in simQuery, (Entity entity, ref GenName _, ref Wallet _) => sims.Add(entity));

        var total = Money.Zero;
        foreach (var entity in sims)
        {
            if (!world.IsAlive(entity))
                continue;

            var state = world.Has<ConsumptionState>(entity)
                ? world.Get<ConsumptionState>(entity)
                : new ConsumptionState();
            state.StartMonth();
            state.LastCycleSpent = Money.Zero;
            state.UnmetDemand += budget;
            state.LastSpendTick = tick;

            if (world.Has<ConsumptionState>(entity))
                world.Get<ConsumptionState>(entity) = state;
            else
                world.Add(entity, state);

            total += budget;
        }

        if (sims.Count > 0)
        {
            economy.Emit(new UnmetDemandRecorded(
                sims.Count,
                total.Cents,
                tick));
        }
    }

    private List<Supplier> CollectSuppliers()
    {
        var capacityByCompany = new Dictionary<Entity, int>();
        var officeQuery = new QueryDescription().WithAll<Office>();
        world.Query(in officeQuery, (ref Office office) =>
        {
            if (office.IsClosed)
                return;
            capacityByCompany.TryGetValue(office.Company, out var current);
            capacityByCompany[office.Company] = current + office.Capacity;
        });

        var suppliers = new List<Supplier>();
        var companyQuery = new QueryDescription().WithAll<CompanyName>();
        world.Query(in companyQuery, (Entity entity, ref CompanyName _) =>
        {
            if (!world.IsAlive(entity))
                return;

            var employees = economy.Employment.CountEmployees(entity);
            capacityByCompany.TryGetValue(entity, out var capacity);
            var weight = 1 + employees * 7 + capacity * 3;
            suppliers.Add(new Supplier(
                entity,
                CompanyAccountService.NetworkIdOf(world, entity),
                weight,
                employees,
                capacity));
        });

        return suppliers;
    }

    private static Supplier? ChooseSupplier(List<Supplier> suppliers, Random random)
    {
        var total = 0;
        foreach (var supplier in suppliers)
            total += supplier.Weight;

        if (total <= 0)
            return null;

        var roll = random.Next(total);
        foreach (var supplier in suppliers)
        {
            roll -= supplier.Weight;
            if (roll < 0)
                return supplier;
        }

        return suppliers[^1];
    }

    private sealed record Supplier(Entity Company, ulong NetworkId, int Weight, int Employees, int Capacity);
}

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Consumption;
using Genelife.Components.Housing;
using Genelife.Components.Player;
using Genelife.Economy;
using Genelife.Family;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Monthly needs-driven consumer spending. Cash is debited only when a company
/// can supply the category; otherwise unmet demand is recorded and no money is
/// destroyed. Food and hygiene are protected before discretionary categories.
/// </summary>
public class ConsumerSpendingSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription genosQuery;
    private readonly int interval;

    public ConsumerSpendingSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
        genosQuery = new QueryDescription().WithAll<GenName, Wallet>().WithNone<Components.Gen.Dead>();
    }

    public string Name => "ConsumerSpending";
    public int Order => 506;
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
        world.Query(in genosQuery, (Entity entity, ref GenName _, ref Wallet _) => spenders.Add(entity));

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

    private (Money Spent, Money Unmet) Spend(long tick, Entity genos, List<Supplier> suppliers, Random random)
    {
        var payer = PayerFor(genos);
        ref var wallet = ref world.Get<Wallet>(payer);
        var state = world.Has<ConsumptionState>(genos)
            ? world.Get<ConsumptionState>(genos)
            : new ConsumptionState();

        state.StartMonth();

        var options = economy.Options;
        var budget = options.MonthlyConsumptionBudget;

        // Savings above the emergency floor are spendable. Without this, a
        // household can accumulate years of savings while consumption stays
        // liquidity-constrained to the wallet balance.
        ExplicitlyWithdrawForConsumption(tick, payer, budget);

        var available = wallet.Balance.Min(budget);
        var spent = Money.Zero;
        var unmet = Money.Zero;

        var requiredTarget = budget.Multiply(options.FoodShare + options.HygieneShare);
        var requiredBudget = available.Min(requiredTarget);

        // Split the essential budget proportionally between food and hygiene.
        // Giving food its full share first starved hygiene completely whenever the
        // household had less than the food share available: the hygiene purchase
        // flag stayed false for the month and blocked showering. The remainder is
        // still protected before the discretionary categories.
        var essentialShare = options.FoodShare + options.HygieneShare;
        var foodRatio = essentialShare <= 0m ? 0m : options.FoodShare / essentialShare;
        var foodBudget = requiredBudget.Multiply(foodRatio);
        var hygieneBudget = requiredBudget - foodBudget;

        spent += Buy("Food", foodBudget, genos, payer, suppliers, random, ref state, ref unmet, tick);
        spent += Buy("Hygiene", hygieneBudget, genos, payer, suppliers, random, ref state, ref unmet, tick);

        var remaining = available - requiredBudget;
        if (remaining.IsPositive)
        {
            var leisureBudget = remaining.Min(budget.Multiply(options.LeisureShare));
            spent += Buy("Leisure", leisureBudget, genos, payer, suppliers, random, ref state, ref unmet, tick);
            remaining -= leisureBudget;

            var healthBudget = remaining.Min(budget.Multiply(options.HealthShare));
            spent += Buy("Health", healthBudget, genos, payer, suppliers, random, ref state, ref unmet, tick);
            remaining -= healthBudget;

            var durableBudget = remaining;
            spent += Buy("Durable", durableBudget, genos, payer, suppliers, random, ref state, ref unmet, tick);
        }

        if (available < budget)
            unmet += budget - available;

        economy.ConsumptionThisYear += spent;

        state.LastCycleSpent = spent;
        state.LifetimeSpent += spent;
        state.UnmetDemand += unmet;
        state.LastSpendTick = tick;

        if (world.Has<ConsumptionState>(genos))
            world.Get<ConsumptionState>(genos) = state;
        else
            world.Add(genos, state);

        return (spent, unmet);
    }

    private Entity PayerFor(Entity genos)
    {
        var designated = DesignatedPayer(genos);
        if (world.Has<Wallet>(designated) && world.Get<Wallet>(designated).Balance.IsPositive)
            return designated;

        // Households share cash. A genos whose own wallet has just been emptied by rent
        // or a utility bill is still fed by the household, so fall back to the richest
        // solvent member rather than recording unmet demand for a household that can pay.
        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        if (household == Entity.Null)
            return designated;

        var solvent = Entity.Null;
        var best = Money.Zero;
        foreach (var member in economy.Housing.GetMembers(household))
        {
            if (!world.IsAlive(member) || !world.Has<Wallet>(member))
                continue;

            var balance = world.Get<Wallet>(member).Balance;
            if (balance.IsPositive && balance > best)
            {
                solvent = member;
                best = balance;
            }
        }

        return solvent != Entity.Null ? solvent : designated;
    }

    /// <summary>
    /// Who is supposed to pay before any household pooling is considered: the genos
    /// itself, or its guardian when it is a dependent child.
    /// </summary>
    private Entity DesignatedPayer(Entity genos)
    {
        if (!ChildhoodPolicy.IsDependentChild(world, genos, economy.Life.Policy))
            return genos;

        var guardian = ChildhoodPolicy.GuardianOf(world, economy, genos);
        return guardian != Entity.Null && world.IsAlive(guardian) && world.Has<Wallet>(guardian)
            ? guardian
            : genos;
    }

    private void ExplicitlyWithdrawForConsumption(long tick, Entity genos, Money budget)
    {
        if (!world.Has<SavingsAccount>(genos))
            return;

        ref var wallet = ref world.Get<Wallet>(genos);
        if (wallet.Balance >= budget)
            return;

        ref var savings = ref world.Get<SavingsAccount>(genos);
        var floor = economy.Options.SavingsBuffer;
        var drawable = savings.Balance - floor;
        if (!drawable.IsPositive)
            return;

        var withdraw = (budget - wallet.Balance).Min(drawable);
        if (!withdraw.IsPositive)
            return;

        savings.Balance -= withdraw;
        savings.LifetimeWithdrawn += withdraw;
        wallet.Balance += withdraw;
        var networkId = CompanyAccountService.NetworkIdOf(world, genos);
        economy.Ledger.Record(
            Guid.NewGuid(),
            tick,
            LedgerKind.SavingsWithdrawal,
            networkId,
            networkId,
            withdraw,
            "savings.consumption");
        economy.Emit(new SavingsWithdrawn(networkId, withdraw.Cents, tick));
    }

    private Money Buy(
        string category,
        Money budget,
        Entity genos,
        Entity payer,
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

        ref var wallet = ref world.Get<Wallet>(payer);
        var amount = budget.Min(wallet.Balance);
        if (!amount.IsPositive)
            return Money.Zero;

        wallet.Balance -= amount;
        economy.BookRevenue(
            supplier.Company,
            category,
            CompanyAccountService.NetworkIdOf(world, payer),
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
        var genoses = new List<Entity>();
        world.Query(in genosQuery, (Entity entity, ref GenName _, ref Wallet _) => genoses.Add(entity));

        var total = Money.Zero;
        foreach (var entity in genoses)
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

        if (genoses.Count > 0)
        {
            economy.Emit(new UnmetDemandRecorded(
                genoses.Count,
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

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Consumption;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Consumption;

public readonly record struct RestockOutcome(int Units, Money Cost, bool Needed)
{
    public bool Restocked => Units > 0;
}

/// <summary>
/// Store inventory creation and lookup. Every store holds one inventory entity
/// per catalog item; stock and price live there, never on the catalog.
/// </summary>
public sealed class GroceryStoreService
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly Func<Entity, NetworkId> allocateNetworkId;
    private readonly GroceryOptions options;
    private readonly int maxStock;
    private readonly Dictionary<(Entity Store, int Item), Entity> inventoryIndex = new();
    private bool indexLoaded;

    public GroceryStoreService(
        World world,
        EconomyContext economy,
        Func<Entity, NetworkId> allocateNetworkId,
        GroceryOptions? options = null)
    {
        this.world = world;
        this.economy = economy;
        this.allocateNetworkId = allocateNetworkId;
        this.options = options ?? GroceryOptions.Default;
        maxStock = this.options.StockPerItem;
    }

    public void EnsureInventory(Entity store, long tick)
    {
        for (var index = 0; index < GroceryCatalog.Items.Count; index++)
        {
            if (FindInventory(store, index, out _, out _))
                continue;

            var item = GroceryCatalog.Items[index];
            var entity = world.Create(
                new StoreInventory
                {
                    ItemIndex = index,
                    Stock = maxStock,
                    CurrentPrice = GroceryPricingPolicy.Price(item, maxStock, maxStock, 0, options),
                    LastRestockTick = tick
                },
                new StoreStockLink { Store = store });
            var id = allocateNetworkId(entity);
            world.Add(entity, id);
            inventoryIndex[(store, index)] = entity;
        }
    }

    private void EnsureIndex()
    {
        if (indexLoaded)
            return;
        indexLoaded = true;
        world.Query(in new QueryDescription().WithAll<StoreInventory, StoreStockLink>(),
            (Entity entity, ref StoreInventory inventory, ref StoreStockLink link) =>
                inventoryIndex[(link.Store, inventory.ItemIndex)] = entity);
    }

    public bool FindInventory(Entity store, int itemIndex, out Entity entity, out StoreInventory inventory)
    {
        EnsureIndex();
        if (inventoryIndex.TryGetValue((store, itemIndex), out var indexed) &&
            world.IsAlive(indexed) && world.Has<StoreInventory>(indexed))
        {
            entity = indexed;
            inventory = world.Get<StoreInventory>(indexed);
            return true;
        }

        var found = Entity.Null;
        var foundInventory = default(StoreInventory);
        world.Query(in new QueryDescription().WithAll<StoreInventory, StoreStockLink>(),
            (Entity candidate, ref StoreInventory data, ref StoreStockLink link) =>
            {
                if (found != Entity.Null || link.Store != store || data.ItemIndex != itemIndex)
                    return;
                found = candidate;
                foundInventory = data;
            });
        entity = found;
        inventory = foundInventory;
        if (found != Entity.Null)
            inventoryIndex[(store, itemIndex)] = found;
        return found != Entity.Null;
    }

    public void Restock(long tick, long monthOfYear)
    {
        var stores = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<Components.Company.Office>(),
            (Entity entity, ref Components.Company.Office _) => stores.Add(entity));
        stores.Sort((a, b) => a.Id.CompareTo(b.Id));

        var companies = new List<Entity>();
        var seenCompanies = new HashSet<Entity>();
        foreach (var store in stores)
        {
            var office = world.Get<Components.Company.Office>(store);
            if (office.IsClosed || office.Company == Entity.Null ||
                !world.IsAlive(office.Company) || !seenCompanies.Add(office.Company))
                continue;
            companies.Add(office.Company);
        }

        Entity SupplierFor(Entity company)
        {
            foreach (var candidate in companies)
                if (candidate != company)
                    return candidate;
            return Entity.Null;
        }

        foreach (var store in stores)
        {
            EnsureInventory(store, tick);
            var storeCompany = world.Has<Components.Company.Office>(store)
                ? world.Get<Components.Company.Office>(store).Company
                : Entity.Null;
            var supplier = SupplierFor(storeCompany);
            for (var index = 0; index < GroceryCatalog.Items.Count; index++)
            {
                if (!FindInventory(store, index, out var entity, out var inventory))
                    continue;
                if (inventory.Stock >= options.RestockThreshold + options.RestockAmount)
                    continue;

                var item = GroceryCatalog.Items[index];
                var units = Math.Min(options.RestockAmount, maxStock - inventory.Stock);
                if (units <= 0)
                    continue;
                var cost = item.BasePrice * units;
                if (!PaySupplier(storeCompany, supplier, cost, tick))
                    continue;

                inventory.Stock += units;
                inventory.LastRestockTick = tick;
                inventory.CurrentPrice = GroceryPricingPolicy.Price(
                    item, inventory.Stock, maxStock, (int)(monthOfYear % 12), options);
                world.Get<StoreInventory>(entity) = inventory;
                economy.Emit(new GroceryRestocked(GetNetworkId(store), units, cost.Cents, tick));
            }
        }
    }

    public RestockOutcome RestockStore(Entity store, long tick, int monthOfYear = 0)
    {
        EnsureInventory(store, tick);
        var storeCompany = world.Has<Components.Company.Office>(store)
            ? world.Get<Components.Company.Office>(store).Company
            : Entity.Null;
        var supplier = FindSupplier(storeCompany);
        var restockedUnits = 0;
        var restockCost = Money.Zero;
        var needed = false;
        for (var index = 0; index < GroceryCatalog.Items.Count; index++)
        {
            if (!FindInventory(store, index, out var entity, out var inventory))
                continue;
            if (inventory.Stock >= options.RestockThreshold + options.RestockAmount)
                continue;
            var item = GroceryCatalog.Items[index];
            var units = Math.Min(options.RestockAmount, maxStock - inventory.Stock);
            if (units <= 0)
                continue;
            needed = true;
            var cost = item.BasePrice * units;
            if (!PaySupplier(storeCompany, supplier, cost, tick))
                continue;
            inventory.Stock += units;
            inventory.LastRestockTick = tick;
            inventory.CurrentPrice = GroceryPricingPolicy.Price(
                item, inventory.Stock, maxStock, (int)(monthOfYear % 12), options);
            world.Get<StoreInventory>(entity) = inventory;
            economy.Emit(new GroceryRestocked(GetNetworkId(store), units, cost.Cents, tick));
            restockedUnits += units;
            restockCost += cost;
        }
        return new RestockOutcome(restockedUnits, restockCost, needed);
    }

    public void Spoil(long tick)
    {
        var inventories = new List<(Entity Entity, StoreInventory Inventory, Entity Store)>();
        world.Query(in new QueryDescription().WithAll<StoreInventory, StoreStockLink>(),
            (Entity entity, ref StoreInventory inventory, ref StoreStockLink link) =>
                inventories.Add((entity, inventory, link.Store)));
        inventories.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

        foreach (var (entity, inventory, store) in inventories)
        {
            if (inventory.Stock <= 0 || inventory.ItemIndex < 0 ||
                inventory.ItemIndex >= GroceryCatalog.Items.Count)
                continue;
            var item = GroceryCatalog.Items[inventory.ItemIndex];
            if (tick - inventory.LastRestockTick <= item.ShelfLifeTicks + options.SpoilageGraceTicks)
                continue;

            economy.Emit(new GrocerySpoiled(
                GetNetworkId(store), item.Id, inventory.Stock, "shelf_life", tick));
            var updated = inventory;
            updated.Stock = 0;
            world.Get<StoreInventory>(entity) = updated;
        }
    }

    public Money PriceFor(Entity store, int itemIndex, int monthOfYear)
    {
        if (!FindInventory(store, itemIndex, out _, out var inventory))
            return GroceryCatalog.Items[itemIndex].BasePrice;
        var item = GroceryCatalog.Items[itemIndex];
        return GroceryPricingPolicy.Price(item, inventory.Stock, maxStock, monthOfYear, options);
    }

    /// <summary>
    /// Quotes a sale without mutating stock — the price the applying path's <see cref="TrySell"/>
    /// would charge for the same units, so an action catalogue can report a cost it will honour.
    /// A store whose inventory has not been materialised yet quotes the same first-restock price
    /// and full stock that <see cref="EnsureInventory"/> creates.
    /// </summary>
    public bool TryQuote(Entity store, int itemIndex, int units, out Money total)
    {
        total = Money.Zero;
        if (units <= 0 || itemIndex < 0 || itemIndex >= GroceryCatalog.Items.Count)
            return false;

        if (FindInventory(store, itemIndex, out _, out var inventory))
        {
            if (inventory.Stock < units)
                return false;
            total = inventory.CurrentPrice * units;
            return true;
        }

        var item = GroceryCatalog.Items[itemIndex];
        total = GroceryPricingPolicy.Price(item, maxStock, maxStock, 0, options) * units;
        return true;
    }

    public bool TrySell(Entity store, int itemIndex, int units, out Money price)
    {
        price = Money.Zero;
        if (units <= 0 || itemIndex < 0 || itemIndex >= GroceryCatalog.Items.Count)
            return false;
        if (!FindInventory(store, itemIndex, out var entity, out var inventory) ||
            inventory.Stock < units)
            return false;

        price = inventory.CurrentPrice * units;
        inventory.Stock -= units;
        world.Get<StoreInventory>(entity) = inventory;
        return true;
    }

    /// <summary>
    /// Picks the lowest network id office that belongs to a different company.
    /// A store cannot supply itself, so the previous "first office in the list"
    /// lookup silently skipped every restock when that office was the store.
    /// </summary>
    private Entity FindSupplier(Entity storeCompany)
    {
        var candidates = new List<(Entity Company, ulong Order)>();
        world.Query(in new QueryDescription().WithAll<Components.Company.Office>(),
            (Entity entity, ref Components.Company.Office office) =>
            {
                if (office.IsClosed)
                    return;
                var company = office.Company;
                if (company == Entity.Null || company == storeCompany || !world.IsAlive(company))
                    return;
                candidates.Add((company, GetNetworkId(entity)));
            });

        candidates.Sort((a, b) =>
        {
            var byId = a.Order.CompareTo(b.Order);
            return byId != 0 ? byId : a.Company.Id.CompareTo(b.Company.Id);
        });
        return candidates.Count > 0 ? candidates[0].Company : Entity.Null;
    }

    private bool PaySupplier(Entity storeCompany, Entity supplier, Money cost, long tick)
    {
        if (!cost.IsPositive)
            return true;
        if (supplier == storeCompany)
            return false;

        var payer = storeCompany;
        ulong payerId;
        if (payer != Entity.Null && world.IsAlive(payer) && economy.Accounts.TryGet(payer, out var account))
        {
            if (account.Balance < cost)
                return false;
            account.Balance -= cost;
            economy.Accounts.Set(payer, account);
            payerId = GetNetworkId(payer);
        }
        else
        {
            if (economy.Government.Balance < cost)
                return false;
            economy.Government.Balance -= cost;
            payerId = EconomyAccountIds.Government;
        }

        // The supplier is a real company; the transfer is ledgered and keeps
        // total money constant (no mint on restock). With no in-world supplier
        // the cost leaves the tracked economy as an import, which is still a
        // real expense and never a mint.
        ulong? payeeId = null;
        if (supplier != Entity.Null && world.IsAlive(supplier))
        {
            economy.Accounts.ApplySettlement(supplier, cost, tick, revenue: true);
            economy.RevenueThisYear += cost;
            payeeId = GetNetworkId(supplier);
        }

        economy.Ledger.Record(
            Guid.NewGuid(),
            tick,
            Components.Finance.LedgerKind.ConsumerSpending,
            payerId,
            payeeId,
            cost,
            "grocery.restock");
        return true;
    }

    private ulong GetNetworkId(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}

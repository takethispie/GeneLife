using Arch.Core;
using Genelife.Api.Commands;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Consumption;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Consumption;
using Genelife.Economy;
using Genelife.Values;

namespace Genelife.UnitTests.Consumption;

public class GroceryTests
{
    [Fact]
    public void InventorySellOutRestockAndSpoilageAreConserved()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var ids = new NetworkIdRegistry();
        var supplier = world.Create(
            new CompanyName("Supplier"),
            CompanyAccount.Create(Money.FromUnits(1_000m), 0));
        ids.Allocate(supplier);
        var supplierOffice = world.Create(new Office("Supplier HQ", supplier));
        ids.Allocate(supplierOffice);
        var company = world.Create(
            new CompanyName("Grocer"),
            CompanyAccount.Create(Money.FromUnits(1_000m), 0));
        ids.Allocate(company);
        var store = world.Create(new Office("Corner Store", company));
        ids.Allocate(store);
        var options = new GroceryOptions { StockPerItem = 3, RestockThreshold = 1, RestockAmount = 2 };
        var stores = new GroceryStoreService(world, economy, entity => ids.Allocate(entity), options);
        stores.EnsureInventory(store, 0);

        Assert.True(stores.FindInventory(store, 0, out var breadEntity, out var bread));
        Assert.Equal(3, bread.Stock);
        Assert.Equal(GroceryCatalog.Version, 1);
        var price = stores.PriceFor(store, 0, 0);
        Assert.True(price.IsPositive);

        Assert.True(stores.TrySell(store, 0, 2, out var sale));
        Assert.Equal(price * 2, sale);
        Assert.True(stores.FindInventory(store, 0, out _, out var afterSale));
        Assert.Equal(1, afterSale.Stock);

        Assert.False(stores.TrySell(store, 0, 2, out _));
        var stockBeforeRestock = afterSale.Stock;
        stores.Restock(10, 1);
        Assert.True(stores.FindInventory(store, 0, out _, out var restocked));
        Assert.True(restocked.Stock > stockBeforeRestock);
        Assert.True(world.IsAlive(company));
        var account = world.Get<CompanyAccount>(company);
        Assert.True(account.Balance >= Money.Zero);

        var old = restocked;
        old.LastRestockTick = -10_000;
        world.Get<StoreInventory>(breadEntity) = old;
        stores.Spoil(10_000);
        Assert.True(stores.FindInventory(store, 0, out _, out var spoiled));
        Assert.Equal(0, spoiled.Stock);
    }

    [Fact]
    public void RestockStoreRestoresStockWithoutAnExternalSupplier()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var ids = new NetworkIdRegistry();
        var company = world.Create(
            new CompanyName("Grocer"),
            CompanyAccount.Create(Money.FromUnits(1_000m), 0));
        ids.Allocate(company);
        var store = world.Create(new Office("Corner Store", company));
        ids.Allocate(store);
        var options = new GroceryOptions { StockPerItem = 3, RestockThreshold = 1, RestockAmount = 2 };
        var stores = new GroceryStoreService(world, economy, entity => ids.Allocate(entity), options);
        stores.EnsureInventory(store, 0);

        Assert.True(stores.FindInventory(store, 0, out var itemEntity, out var soldOut));
        soldOut.Stock = 0;
        world.Get<StoreInventory>(itemEntity) = soldOut;
        var balanceBefore = world.Get<CompanyAccount>(company).Balance;

        var outcome = stores.RestockStore(store, 10, 1);

        Assert.True(outcome.Restocked);
        Assert.True(stores.FindInventory(store, 0, out _, out var restocked));
        Assert.True(restocked.Stock > 0);
        Assert.True(world.Get<CompanyAccount>(company).Balance < balanceBefore);

        world.Dispose();
    }

    [Fact]
    public void AdminRestockCommandAppliesToTheStore()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong storeId = 0;
        var store = Entity.Null;
        engine.ExecuteWithWorld(world =>
        {
            var company = world.Create(
                new CompanyName("Grocer"),
                CompanyAccount.Create(Money.FromUnits(1_000m), 0));
            world.Add(company, engine.NetworkIds.Allocate(company));
            store = world.Create(new Office("Corner Store", company));
            storeId = engine.NetworkIds.Allocate(store).Value;
            world.Add(store, new NetworkId(storeId));

            var stores = new GroceryStoreService(
                world,
                engine.Economy!,
                entity => engine.NetworkIds.AllocateInfrastructure(entity));
            stores.EnsureInventory(store, 0);
            Assert.True(stores.FindInventory(store, 0, out var itemEntity, out var inventory));
            inventory.Stock = 0;
            world.Get<StoreInventory>(itemEntity) = inventory;
            return 0;
        });

        var commandId = engine.EnqueueAdminCommand(new RestockStoreCommand(
            Guid.NewGuid(), "tester", storeId, "test-restock"));
        engine.Tick(1f / 20f);

        var result = engine.GetCommandResult(commandId);
        Assert.NotNull(result);
        Assert.True(result!.Accepted, result.Error);

        engine.ExecuteWithWorld(world =>
        {
            var stores = new GroceryStoreService(
                world,
                engine.Economy!,
                entity => engine.NetworkIds.AllocateInfrastructure(entity));
            Assert.True(stores.FindInventory(store, 0, out _, out var inventory));
            Assert.True(inventory.Stock > 0);
            return 0;
        });
    }

    [Fact]
    public void PricingIsDeterministicClampedAndStockSensitive()
    {
        var item = GroceryCatalog.Items[0];
        var empty = GroceryPricingPolicy.Price(item, 0, 20, 0);
        var full = GroceryPricingPolicy.Price(item, 20, 20, 0);
        Assert.Equal(empty, GroceryPricingPolicy.Price(item, 0, 20, 0));
        Assert.True(empty > full);
        Assert.True(empty <= item.BasePrice * 1.40m);
        Assert.True(full >= item.BasePrice * 0.80m);
    }

    [Fact]
    public void PlayerGroceryPurchaseUsesStockAndDoesNotCreateMoney()
    {
        using var fixture = new PlayerGroceryFixture();
        var beforeSupply = fixture.Economy.TotalMoneySupply();
        var beforeWallet = fixture.Wallet;

        var result = fixture.Actions.Apply(
            "p1",
            fixture.Sim,
            Genelife.Api.Commands.PlayerActionKind.BuyItem,
            System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                storeNetworkId = fixture.StoreNetworkId,
                catalogItemId = "bread",
                quantity = 1
            }),
            tick: 10);

        Assert.True(result.Applied, result.Code);
        Assert.True(fixture.Wallet < beforeWallet);
        Assert.Equal(beforeSupply, fixture.Economy.TotalMoneySupply());
        Assert.True(fixture.StoreService.FindInventory(fixture.Store, 0, out _, out var stock));
        Assert.True(stock.Stock < fixture.StockPerItem);
    }

    private sealed class PlayerGroceryFixture : IDisposable
    {
        private readonly World world = World.Create();
        public EconomyContext Economy { get; }
        private readonly NetworkIdRegistry ids = new();
        public PlayerActionService Actions { get; }
        public Entity Store { get; }
        public GroceryStoreService StoreService { get; }
        public ulong StoreNetworkId { get; }
        public Entity Sim { get; }
        public int StockPerItem => GroceryOptions.Default.StockPerItem;
        public Money Wallet => world.Get<Wallet>(Sim).Balance;

        public PlayerGroceryFixture()
        {
            Economy = new EconomyContext(world);
            Economy.Housing.AttachNetworkIdAllocator(entity => ids.Allocate(entity));
            var relationships = new Genelife.Family.RelationshipIndex();
            var relationshipService = new Genelife.Family.RelationshipService(world, relationships)
            {
                NetworkIdAllocator = entity => ids.Allocate(entity),
                NetworkIdRemover = entity => ids.Remove(entity)
            };
            var pregnancies = new Genelife.Family.PregnancyIndex();
            pregnancies.AttachWorld(world);
            var pregnancyService = new Genelife.Family.PregnancyService(world, pregnancies)
            {
                NetworkIdAllocator = entity => ids.Allocate(entity),
                NetworkIdRemover = entity => ids.Remove(entity)
            };
            var company = world.Create(new CompanyName("Grocer"), CompanyAccount.Create(Money.FromUnits(1_000m), 0));
            Register(company, 100);
            Store = world.Create(new Office("Store", company));
            StoreNetworkId = Register(Store, 101);
            Sim = world.Create(
                new Genelife.Components.Gen.GenName("Shopper"),
                new Genelife.Components.Gen.Needs { Hunger = 20f },
                new Genelife.Components.Gen.Alive(30),
                new Genelife.Components.Gen.PlayerControlled("p1"),
                new Wallet(Money.FromUnits(100m)),
                new Genelife.Components.Life.Lifecycle(Genelife.Enums.LifeStage.Work, 0, 0));
            Register(Sim, 102);
            var players = new Genelife.Player.PlayerRegistry();
            players.GetOrCreate("p1", Sim, 0);
            Actions = new PlayerActionService(
                world,
                Economy,
                ids,
                players,
                relationships,
                relationshipService,
                pregnancies,
                pregnancyService,
                Genelife.Family.RelationshipOptions.Default,
                Genelife.Family.ReproductionOptions.Default);
            StoreService = new GroceryStoreService(world, Economy, entity => ids.Allocate(entity));
            StoreService.EnsureInventory(Store, 0);
        }

        private ulong Register(Entity entity, ulong id)
        {
            world.Add(entity, new Genelife.Components.Gen.NetworkId(id));
            ids.AllocateWithValue(entity, new Genelife.Components.Gen.NetworkId(id));
            return id;
        }

        public void Dispose() => world.Dispose();
    }
}

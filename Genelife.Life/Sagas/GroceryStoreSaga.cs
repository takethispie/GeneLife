using System.Linq.Expressions;
using Genelife.Global.Messages.DTOs;
using Genelife.Life.Messages.Commands;
using Genelife.Life.Messages.Commands.Grocery;
using Genelife.Life.Messages.Commands.Locomotion;
using Genelife.Life.Messages.Events.Buildings;
using Genelife.Life.Messages.Events.Grocery;
using Genelife.Life.Messages.Events.Locomotion;
using MassTransit;
using Serilog;

namespace Genelife.Life.Sagas;

public class GroceryStoreSaga :
    ISaga,
    ISagaVersion,
    InitiatedBy<GroceryStoreBuilt>,
    Orchestrates<GoToGroceryStore>,
    Orchestrates<LeaveGroceryStore>,
    Orchestrates<BuyFood>,
    Orchestrates<BuyDrink>,
    Orchestrates<AddRevenue>,
    Observes<DiscoverGroceryStores, GroceryStoreSaga>
{
    public Guid CorrelationId { get; set; }
    public Position Position { get; set; } = new(0, 0, 0);
    public List<Guid> Customers { get; set; } = [];
    public int FoodPrice { get; set; } = 5;
    public int DrinkPrice { get; set; } = 3;
    public decimal Revenue { get; set; } = 0;
    public bool IsOpen { get; set; } = true;
    public int Version { get; set; }

    public Expression<Func<GroceryStoreSaga, DiscoverGroceryStores, bool>> CorrelationExpression => (saga, message) => true;

    public async Task Consume(ConsumeContext<GroceryStoreBuilt> context)
    {
        Position = new(context.Message.X, context.Message.Y, context.Message.Z);
        FoodPrice = context.Message.FoodPrice;
        DrinkPrice = context.Message.DrinkPrice;
        IsOpen = true;
        Revenue = 0;
        await context.Publish(new GroceryStoreAddressAnnounced(
            CorrelationId,
            context.Message.X, context.Message.Y, context.Message.Z
        ));
        Log.Information("Grocery store {Name} created at position ({X}, {Y}, {Z}) with food price {FoodPrice:C} and drink price {DrinkPrice:C}",
            context.Message.Name, context.Message.X, context.Message.Y, context.Message.Z, context.Message.FoodPrice, context.Message.DrinkPrice);
    }

    public async Task Consume(ConsumeContext<GoToGroceryStore> context)
    {
        if (Customers.Contains(context.Message.HumanId)) return;
        Customers.Add(context.Message.HumanId);
        await context.Publish(new EnteredGroceryStore(context.Message.HumanId, CorrelationId));
        Log.Information("Customer {CustomerId} entered grocery store {StoreId}",
            context.Message.HumanId, CorrelationId);
    }

    public async Task Consume(ConsumeContext<LeaveGroceryStore> context)
    {
        if (!Customers.Contains(context.Message.HumanId)) return;
        Customers.Remove(context.Message.HumanId);
        await context.Publish(new LeftGroceryStore(context.Message.HumanId, CorrelationId));
        Log.Information("Customer {CustomerId} left grocery store {StoreId}",
            context.Message.HumanId, CorrelationId);
    }

    public async Task Consume(ConsumeContext<BuyFood> context)
    {
        if (!Customers.Contains(context.Message.HumanId)) return;
        await context.Publish(new FoodPurchased(context.Message.HumanId, CorrelationId, FoodPrice));
    }

    public async Task Consume(ConsumeContext<BuyDrink> context)
    {
        if (!Customers.Contains(context.Message.HumanId)) return;
        await context.Publish(new DrinkPurchased(context.Message.HumanId, CorrelationId, DrinkPrice));
    }

    public Task Consume(ConsumeContext<AddRevenue> context)
    {
        Revenue += context.Message.Amount;
        Log.Information("Grocery store {StoreId} received revenue of {Amount:C}, total revenue: {Revenue:C}",
            CorrelationId, context.Message.Amount, Revenue);
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<DiscoverGroceryStores> context)
    {
        await context.Publish(new AddGroceryStoreAddress(
            context.Message.HumanId,
            CorrelationId,
            Position.X,
            Position.Y,
            Position.Z
        ));
        Log.Information("Grocery store {StoreId} responded to discovery request from human {HumanId}",
            CorrelationId, context.Message.HumanId);
    }
}

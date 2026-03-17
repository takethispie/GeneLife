using System.Linq.Expressions;
using Genelife.Application.IntegrationEvents;
using Genelife.Domain;
using Genelife.Domain.Grocery;
using Genelife.Messages.Commands;
using Genelife.Messages.Commands.Grocery;
using Genelife.Messages.Commands.Locomotion;
using Genelife.Messages.Events.Buildings;
using Genelife.Messages.Events.Grocery;
using Genelife.Messages.Events.Locomotion;
using MassTransit;
using Serilog;

namespace Genelife.Application.Sagas;

public class GroceryStoreSaga :
    ISagaVersion,
    InitiatedBy<GroceryStoreBuilt>,
    Orchestrates<GoToGroceryStore>,
    Orchestrates<LeaveGroceryStore>,
    Orchestrates<BuyFood>,
    Orchestrates<BuyDrink>,
    Observes<DiscoverGroceryStores, GroceryStoreSaga>
{
    public Guid CorrelationId { get; set; }
    public GroceryStore Store { get; set; }
    public int Version { get; set; }

    public Expression<Func<GroceryStoreSaga, DiscoverGroceryStores, bool>> CorrelationExpression => (saga, message) => true;

    public async Task Consume(ConsumeContext<GroceryStoreBuilt> context)
    {
        Store = new GroceryStore(
            context.CorrelationId ??  Guid.NewGuid(), 
            new(context.Message.X, context.Message.Y, context.Message.Z),
            context.Message.FoodPrice,
            context.Message.DrinkPrice
        );
        await context.Publish(new GroceryStoreAddressAnnounced(
            CorrelationId,
            context.Message.X, context.Message.Y, context.Message.Z
        ));
        Log.Information("Grocery store {Name} created at position ({X}, {Y}, {Z}) with food price {FoodPrice:C} and drink price {DrinkPrice:C}",
            context.Message.Name, context.Message.X, context.Message.Y, context.Message.Z, context.Message.FoodPrice, context.Message.DrinkPrice);
    }

    public async Task Consume(ConsumeContext<GoToGroceryStore> context)
    {
        Store.CustomerEnters(context.Message.HumanId);
        await context.Publish(new EnteredGroceryStore(context.Message.HumanId, CorrelationId));
        await context.Publish(GroceryUpdate.FromStore(CorrelationId, Store));
        Log.Information("Customer {CustomerId} entered grocery store {StoreId}",
            context.Message.HumanId, CorrelationId);
    }

    public async Task Consume(ConsumeContext<LeaveGroceryStore> context)
    {
        Store.CustomerLeaves(context.Message.HumanId);
        await context.Publish(new LeftGroceryStore(context.Message.HumanId, CorrelationId));
        await context.Publish(GroceryUpdate.FromStore(CorrelationId, Store));
        Log.Information("Customer {CustomerId} left grocery store {StoreId}",
            context.Message.HumanId, CorrelationId);
    }

    public async Task Consume(ConsumeContext<BuyFood> context)
    {
        if (!Store.BuyFood(context.Message.HumanId)) return;
        await context.Publish(new FoodPurchased(context.Message.HumanId, CorrelationId, Store.FoodPrice));
        await context.Publish(GroceryUpdate.FromStore(CorrelationId, Store));
    }

    public async Task Consume(ConsumeContext<BuyDrink> context)
    {
        if (!Store.BuyDrink(context.Message.HumanId)) return;
        await context.Publish(new DrinkPurchased(context.Message.HumanId, CorrelationId, Store.DrinkPrice));
        await context.Publish(GroceryUpdate.FromStore(CorrelationId, Store));
    }

    public async Task Consume(ConsumeContext<DiscoverGroceryStores> context)
    {
        await context.Publish(new AddGroceryStoreAddress(
            context.Message.HumanId,
            CorrelationId,
            Store.Position.X,
            Store.Position.Y,
            Store.Position.Z
        ));
        Log.Information("Grocery store {StoreId} responded to discovery request from human {HumanId}",
            CorrelationId, context.Message.HumanId);
    }
}

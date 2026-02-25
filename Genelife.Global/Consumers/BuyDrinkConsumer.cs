using Genelife.Global.Messages.Events.Grocery;
using Genelife.Life.Messages.Commands;
using MassTransit;
using Serilog;

namespace Genelife.Global.Consumers;

public class BuyDrinkConsumer : IConsumer<DrinkPurchased>
{
    public async Task Consume(ConsumeContext<DrinkPurchased> context)
    {
        var humanId = context.Message.CorrelationId;
        var storeId = context.Message.GroceryStoreId;
        var price = context.Message.Price;

        await context.Publish(new AddMoney(humanId, -(float)price));
        await context.Publish(new AddRevenue(storeId, price));

        Log.Information("Customer {CustomerId} bought drink for {Price:C} at grocery store {StoreId}",
            humanId, price, storeId);
    }
}

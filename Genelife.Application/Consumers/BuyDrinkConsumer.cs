using Genelife.Messages.Commands;
using Genelife.Messages.Events.Grocery;
using MassTransit;
using Serilog;

namespace Genelife.Application.Consumers;

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

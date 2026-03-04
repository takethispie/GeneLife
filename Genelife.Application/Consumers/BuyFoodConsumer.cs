using Genelife.Messages.Commands;
using Genelife.Messages.Events.Grocery;
using MassTransit;
using Serilog;

namespace Genelife.Application.Consumers;

public class BuyFoodConsumer : IConsumer<FoodPurchased>
{
    public async Task Consume(ConsumeContext<FoodPurchased> context)
    {
        var humanId = context.Message.HumanId;
        var storeId = context.Message.GroceryStoreId;
        var price = context.Message.Price;

        await context.Publish(new AddMoney(humanId, -(float)price));
        await context.Publish(new AddRevenue(storeId, price));

        Log.Information("Customer {CustomerId} bought food for {Price:C} at grocery store {StoreId}",
            humanId, price, storeId);
    }
}

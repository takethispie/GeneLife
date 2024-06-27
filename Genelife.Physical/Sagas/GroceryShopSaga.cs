using System.Numerics;
using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Physical.Sagas;

public class GroceryShopSaga : ISaga, InitiatedBy<CreateGroceryShop>, Orchestrates<BuyItems>, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Vector2 Size { get; set; }
    public Vector3 Position { get; set; }
    public int Version { get; set; }

    public Task Consume(ConsumeContext<CreateGroceryShop> context)
    {
        Console.WriteLine($"created Grocery Store {context.Message.CorrelationId} at position {context.Message.X}:{context.Message.Y}");
        Size = new Vector2(context.Message.Width, context.Message.Depth);
        Position = new Vector3(context.Message.X, context.Message.Y, 0);
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<BuyItems> context)
    {
        Console.WriteLine($"{context.Message.CorrelationId} buying items");
        foreach(var item in context.Message.Items) {
            //TODO proper item selection/cost management 
            await context.Publish(new ItemBought(context.Message.Buyer, new Item(0, item.ItemType.ToString(), item.ItemType), 2));
        }
    }
}
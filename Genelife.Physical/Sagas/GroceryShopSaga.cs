using System.Numerics;
using Genelife.Domain.Commands;
using MassTransit;

namespace Genelife.Physical.Sagas;

public class GroceryShopSaga : ISaga, InitiatedBy<CreateGroceryShop>
{
    public Guid CorrelationId { get; set; }
    public Vector2 Size { get; set; }
    public Vector3 Position { get; set; }

    public Task Consume(ConsumeContext<CreateGroceryShop> context)
    {
        Console.WriteLine($"created Grocery Store {context.Message.CorrelationId} at position {context.Message.X}");
        Size = context.Message.Size;
        Position = new Vector3(context.Message.X, context.Message.Y, 0);
        return Task.CompletedTask;
    }
}
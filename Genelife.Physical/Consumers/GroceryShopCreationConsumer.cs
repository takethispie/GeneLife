using System.Numerics;
using Genelife.Domain.Commands;
using Genelife.Physical.Domain;
using Genelife.Physical.Repository;
using MassTransit;

namespace Genelife.Physical.Consumers;

public class GroceryShopCreationConsumer(GroceryShopCache groceryShopRepository) : IConsumer<CreateGroceryShop>
{
    private GroceryShopCache repository = groceryShopRepository;

    public Task Consume(ConsumeContext<CreateGroceryShop> context)
    {
        Console.WriteLine($"storing grocery store: {context.Message.CorrelationId}");
        var msg = context.Message;
        repository.Add(new GroceryShop(msg.CorrelationId, new Vector3(msg.X, msg.Y, 0), msg.Width, msg.Depth ));
        return Task.CompletedTask;
    }
}

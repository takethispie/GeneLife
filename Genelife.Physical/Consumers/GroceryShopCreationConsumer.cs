using Genelife.Domain.Commands;
using Genelife.Physical.Domain;
using Genelife.Physical.Repository;
using MassTransit;

namespace Genelife.Physical.Consumers;

public class GroceryShopCreationConsumer(GroceryShopRepository groceryShopRepository) : IConsumer<CreateGroceryShop>
{
    private GroceryShopRepository repository = groceryShopRepository;

    public Task Consume(ConsumeContext<CreateGroceryShop> context)
    {
        Console.WriteLine($"storing grocery store: {context.Message.CorrelationId}");
        var msg = context.Message;
        repository.Add(new GroceryShop(msg.CorrelationId, msg.Position, msg.Size ));
        return Task.CompletedTask;
    }
}
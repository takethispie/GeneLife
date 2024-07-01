using System.Numerics;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using Genelife.Physical.Domain;
using Genelife.Physical.Repository;
using MassTransit;
using MongoDB.Bson;

namespace Genelife.Physical.Consumers;

public class GroceryFinderConsumer(GroceryShopCache groceryShopRepository) : IConsumer<FindClosestGroceryShop>
{
    private readonly GroceryShopCache repository = groceryShopRepository;

    public async Task Consume(ConsumeContext<FindClosestGroceryShop> context)
    {
        Console.WriteLine($"finding nearest grocery shop for Human {context.Message.CorrelationId}");
        if(repository.GetClosest(context.Message.SourcePosition) is GroceryShop grocery) {
            Console.WriteLine($"grocery found: {grocery.ToJson()}");

            await context.Publish(new ClosestGroceryShopFound(context.Message.CorrelationId, grocery.Position.X, grocery.Position.Y, 0, grocery.Guid));
        }
        else Console.WriteLine($"grocery not found close to position {context.Message.SourcePosition}");
    }

}

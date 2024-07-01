using System.Numerics;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using Genelife.Physical.Domain;
using Genelife.Physical.Repository;
using MassTransit;
using MongoDB.Bson;

namespace Genelife.Physical.Consumers;

public class GroceryFinderConsumer(GroceryShopCache groceryShopRepository, HumanCache humanRepository) : IConsumer<FindClosestGroceryShop>
{
    private readonly GroceryShopCache repository = groceryShopRepository;
    private readonly HumanCache humanRepository = humanRepository;

    public async Task Consume(ConsumeContext<FindClosestGroceryShop> context)
    {
        Console.WriteLine($"finding nearest grocery shop for Human {context.Message.CorrelationId}");
        if(humanRepository.Get(context.Message.CorrelationId) is not Human human)
        {
            Console.WriteLine($"human with id: {context.Message.CorrelationId} not found");
            return;
        }
        if(repository.GetClosest(human.Position) is GroceryShop grocery) {
            Console.WriteLine($"grocery found: {grocery.ToJson()}");

            await context.Publish(new ClosestGroceryShopFound(human.CorrelationId, grocery.Position.X, grocery.Position.Y, 0, grocery.Guid));
        }
        else Console.WriteLine($"grocery not found close to position {human.Position}");
    }

}

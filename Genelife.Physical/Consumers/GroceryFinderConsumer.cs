using System.Numerics;
using Genelife.Domain.Commands;
using Genelife.Domain.Commands.Buildings;
using Genelife.Domain.Events;
using Genelife.Domain.Events.Buildings;
using Genelife.Physical.Domain;
using Genelife.Physical.Repository;
using MassTransit;
using MongoDB.Bson;
using Serilog;

namespace Genelife.Physical.Consumers;

public class GroceryFinderConsumer(GroceryShopCache groceryShopRepository) : IConsumer<FindClosestGroceryShop>
{
    private readonly GroceryShopCache repository = groceryShopRepository;

    public async Task Consume(ConsumeContext<FindClosestGroceryShop> context)
    {
        Log.Information($"finding nearest grocery shop for Human {context.Message.CorrelationId}");
        if(repository.GetClosest(context.Message.SourcePosition) is GroceryShop grocery) {
            Log.Information($"grocery found: {grocery.ToJson()}");
            await context.Publish(new ClosestGroceryShopFound(context.Message.CorrelationId, grocery.Position.X, grocery.Position.Y, 0, grocery.Guid));
        }
        else Log.Information($"grocery not found close to position {context.Message.SourcePosition}");
    }

}

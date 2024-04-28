using Genelife.Domain.Commands;
using Genelife.Physical.Domain;
using Genelife.Physical.Repository;
using MassTransit;

namespace Genelife.Physical.Consumers;

public class GroceryFinderConsumer(GroceryShopCache groceryShopRepository, HumanCache humanRepository) : IConsumer<GoToGroceryShop>
{
    private readonly GroceryShopCache repository = groceryShopRepository;
    private readonly HumanCache humanRepository = humanRepository;

    public async Task Consume(ConsumeContext<GoToGroceryShop> context)
    {
        Console.WriteLine($"finding nearest grocery shop for Human {context.Message.HumanId}");
        if(humanRepository.Get(context.Message.HumanId) is not Human human)
        {
            Console.WriteLine($"human with id: {context.Message.HumanId} not found");
            return;
        }
        if(repository.GetClosest(human.Position) is GroceryShop grocery) 
            await context.Publish(new MoveTo(human.CorrelationId, Convert.ToInt32(grocery.Position.X), Convert.ToInt32(grocery.Position.Y), grocery.Guid));
        else Console.WriteLine($"grocery not found close to position {human.Position}");
    }

}

using Genelife.Domain.Commands;
using Genelife.Physical.Repository;
using MassTransit;

namespace Genelife.Physical.Consumers;

public class GroceryFinderConsumer(GroceryShopRepository groceryShopRepository, HumanRepository humanRepository) : IConsumer<GoToGroceryShop>
{
    private readonly GroceryShopRepository repository = groceryShopRepository;
    private readonly HumanRepository humanRepository = humanRepository;

    public async Task Consume(ConsumeContext<GoToGroceryShop> context)
    {
        var human = humanRepository.Get(context.Message.CorrelationId);
        if(human == null)
        {
            Console.WriteLine($"human with id: {context.Message.CorrelationId} not found");
            return;
        }
        var grocery = repository.GetClosest(human.Position);
        if(grocery == null)
            Console.WriteLine($"grocery not found close to position {human.Position}");
        await context.Publish(new MoveTo(human.CorrelationId, grocery.Position));
        return;
    }

}

﻿using Genelife.Domain.Commands;
using Genelife.Physical.Repository;
using MassTransit;

namespace Genelife.Physical.Consumers;

public class GroceryFinderConsumer(GroceryShopRepository groceryShopRepository, HumanRepository humanRepository) : IConsumer<GoToGroceryShop>
{
    private GroceryShopRepository repository = groceryShopRepository;
    private HumanRepository humanRepository = humanRepository;

    public async Task Consume(ConsumeContext<GoToGroceryShop> context)
    {
        Console.WriteLine($"finding nearest grocery shop for Human {context.Message.HumanId}");
        var human = humanRepository.Get(context.Message.HumanId);
        if(human == null)
        {
            Console.WriteLine($"human with id: {context.Message.HumanId} not found");
            return;
        }
        var grocery = repository.GetClosest(human.Position);
        if(grocery == null)
            Console.WriteLine($"grocery not found close to position {human.Position}");
        await context.Publish(new MoveTo(human.CorrelationId, Convert.ToInt32(grocery.Position.X), Convert.ToInt32(grocery.Position.Y), grocery.Guid));
        return;
    }

}
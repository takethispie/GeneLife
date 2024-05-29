using System.Linq.Expressions;
using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Survival.Sagas;

public class SurvivalSaga : 
    ISaga, 
    InitiatedBy<CreateHuman>, 
    Observes<DayElapsed, SurvivalSaga>, 
    Orchestrates<Eat>, 
    Orchestrates<SetHunger>, 
    Orchestrates<Drink>, 
    Orchestrates<SetThirst>, 
    Orchestrates<ListFoodAndDrinkToBuy>,
    ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public int Hunger { get; set; }
    public int Thirst { get; set; }
    public int Version { get; set; }

    public Expression<Func<SurvivalSaga, DayElapsed, bool>> CorrelationExpression => (_, _) => true;

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"{context.Message.Human.FirstName} {context.Message.Human.LastName}'s hunger set to 0");
        Hunger = 0;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<DayElapsed> context)
    {
        Hunger++;
        Thirst++;
        if (Hunger >= 10)
        {
            Console.WriteLine($"{CorrelationId} is starving");
            await context.Publish(new Starving(CorrelationId));
        }
        else if (Hunger >= 8)
        {
            Console.WriteLine($"{CorrelationId} started being hungry");
            await context.Publish(new StartedBeingHungry(CorrelationId));
        }

        if(Thirst >= 20) {
            Console.WriteLine($"{CorrelationId} is dehydrated");
            await context.Publish(new Dehydrated(CorrelationId));
        }
        else if(Thirst >= 17) {
            Console.WriteLine($"{CorrelationId} started being thirsty");
            await context.Publish(new StartedBeingThirsty(CorrelationId));
        }
        Console.WriteLine($"{CorrelationId} Thirst: {Thirst} Hunger: {Hunger}");
        return;
    }

    public async Task Consume(ConsumeContext<Eat> context)
    {
        Hunger = 0;
        Console.WriteLine($"human {CorrelationId} ate food");
        await context.Publish(new HasEaten(CorrelationId));
    }

    public Task Consume(ConsumeContext<SetHunger> context)
    {
        Hunger = context.Message.Value;
        Console.WriteLine($"set Hunger to {Hunger} for human {CorrelationId}");
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<Drink> context)
    {
        Thirst = 0;
        Console.WriteLine($"human {CorrelationId} drank");
        await context.Publish(new HasDrank(CorrelationId));
    }

    public Task Consume(ConsumeContext<SetThirst> context)
    {
        Thirst = context.Message.Value;
        Console.WriteLine($"set thirst to {Thirst} for human {CorrelationId}");
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<ListFoodAndDrinkToBuy> context)
    {
        GroceryListItem[] items = (Thirst >= 20, Hunger >= 10) switch {
            (true, false) => [new GroceryListItem(ItemType.Drink, 1)],
            (false, true) => [new GroceryListItem(ItemType.Food, 1)],
            (true, true) => [new GroceryListItem(ItemType.Food, 1), new GroceryListItem(ItemType.Drink, 1)],
            (false, false) => []
        };
        Console.WriteLine($"{context.Message.CorrelationId} will buy {items.Length} items");
        if(items is not [])
            await context.Publish(new BuyItems(CorrelationId, items, context.Message.TargetGroceryShop));
    }
}
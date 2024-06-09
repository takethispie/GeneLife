using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Main.Services;

public class SurvivalService {
    public static async void ProcessState(int hunger, int thirst, IPublishEndpoint endpoint, Guid correlationId) {
        hunger++;
        thirst++;

        if (hunger >= 10)
        {
            Console.WriteLine($"{correlationId} is starving");
            await endpoint.Publish(new Starving(correlationId));
        }
        else if (hunger >= 8)
        {
            Console.WriteLine($"{correlationId} started being hungry");
            await endpoint.Publish(new StartedBeingHungry(correlationId));
        }

        if(thirst >= 20) {
            Console.WriteLine($"{correlationId} is dehydrated");
            await endpoint.Publish(new Dehydrated(correlationId));
        }
        else if(thirst >= 17) {
            Console.WriteLine($"{correlationId} started being thirsty");
            await endpoint.Publish(new StartedBeingThirsty(correlationId));
        }
        Console.WriteLine($"{correlationId} Thirst: {thirst} Hunger: {hunger}");
        return;
    }
    

    public static GroceryListItem[] GetNeededItems(int hunger, int thirst) {
        return (thirst >= 20, hunger >= 10) switch {
            (true, false) => [new GroceryListItem(ItemType.Drink, 1)],
            (false, true) => [new GroceryListItem(ItemType.Food, 1)],
            (true, true) => [new GroceryListItem(ItemType.Food, 1), new GroceryListItem(ItemType.Drink, 1)],
            (false, false) => []
        };
    }
}
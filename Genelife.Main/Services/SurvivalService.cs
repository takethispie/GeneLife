using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Main.Services;

public class SurvivalService {
    public async void ProcessState(int hunger, int thirst, IPublishEndpoint endpoint, Guid correlationId) {
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
}
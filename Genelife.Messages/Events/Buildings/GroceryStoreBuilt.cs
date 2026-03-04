using MassTransit;

namespace Genelife.Messages.Events.Buildings;

public record GroceryStoreBuilt(
    Guid CorrelationId,
    float X,
    float Y,
    float Z,
    string Name,
    int FoodPrice = 5,
    int DrinkPrice = 3
) : CorrelatedBy<Guid>;
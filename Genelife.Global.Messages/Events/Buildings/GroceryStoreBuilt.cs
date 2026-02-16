namespace Genelife.Global.Messages.Events.Buildings;

public record GroceryStoreBuilt(
    Guid CorrelationId,
    float X,
    float Y,
    float Z,
    string Name,
    decimal FoodPrice = 5.00m,
    decimal DrinkPrice = 3.00m
);
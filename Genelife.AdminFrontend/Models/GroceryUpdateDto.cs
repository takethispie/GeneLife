namespace Genelife.AdminFrontend.Models;

public record GroceryUpdateDto(
    Guid CorrelationId,
    float X,
    float Y,
    float Z,
    int FoodPrice,
    int DrinkPrice,
    float Revenue,
    int CustomerCount
);

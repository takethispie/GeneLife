namespace Genelife.API.DTOs;

public record CreateGroceryStoreRequest(
    string Name,
    float X,
    float Y,
    float Z,
    int FoodPrice = 4,
    int DrinkPrice = 1
);
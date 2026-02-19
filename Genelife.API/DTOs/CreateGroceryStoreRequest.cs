namespace Genelife.API.DTOs;

public record CreateGroceryStoreRequest(
    string Name,
    float X,
    float Y,
    float Z,
    int FoodPrice = 5,
    int DrinkPrice = 3
);
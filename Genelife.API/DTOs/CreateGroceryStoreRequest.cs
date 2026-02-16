namespace Genelife.API.DTOs;

public record CreateGroceryStoreRequest(
    string Name,
    float X,
    float Y,
    float Z,
    decimal FoodPrice = 5.00m,
    decimal DrinkPrice = 3.00m
);
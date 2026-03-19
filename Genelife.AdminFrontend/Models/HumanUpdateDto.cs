namespace Genelife.AdminFrontend.Models;

public record HumanUpdateDto(
    Guid CorrelationId,
    string FirstName,
    string LastName,
    DateTime Birthday,
    string Sex,
    float Money,
    float Hunger,
    float Energy,
    float Hygiene,
    float Thirst,
    int FoodItemCount,
    int DrinkItemCount,
    string CurrentState,
    float X,
    float Y,
    float Z
);

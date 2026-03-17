using Genelife.Domain.Human;

namespace Genelife.Application.IntegrationEvents;

public record HumanUpdate(
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
)
{
    public static HumanUpdate FromPerson(Person person, string currentState) =>
        new(
            person.Id,
            person.FirstName,
            person.LastName,
            person.Birthday,
            person.Sex.ToString(),
            person.Money,
            person.Hunger,
            person.Energy,
            person.Hygiene,
            person.Thirst,
            person.FoodItemCount,
            person.DrinkItemCount,
            currentState,
            person.Coordinates.X,
            person.Coordinates.Y,
            person.Coordinates.Z
        );
}

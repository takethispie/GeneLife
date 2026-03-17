using Genelife.Domain.Grocery;

namespace Genelife.Application.IntegrationEvents;

public record GroceryUpdate(
    Guid CorrelationId,
    float X,
    float Y,
    float Z,
    int FoodPrice,
    int DrinkPrice,
    float Revenue,
    int CustomerCount
)
{
    public static GroceryUpdate FromStore(Guid correlationId, GroceryStore store) =>
        new(
            correlationId,
            store.Position.X,
            store.Position.Y,
            store.Position.Z,
            store.FoodPrice,
            store.DrinkPrice,
            store.Revenue,
            store.Customers.Count
        );
}

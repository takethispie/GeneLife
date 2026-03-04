using MassTransit;

namespace Genelife.Messages.Events.Grocery;

public record FoodPurchased(Guid HumanId, Guid GroceryStoreId, decimal Price) : CorrelatedBy<Guid>
{
    public Guid CorrelationId => HumanId;
}
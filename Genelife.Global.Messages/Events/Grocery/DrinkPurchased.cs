using MassTransit;

namespace Genelife.Global.Messages.Events.Grocery;

public record DrinkPurchased(Guid HumanId, Guid GroceryStoreId, decimal Price) : CorrelatedBy<Guid>
{
    public Guid CorrelationId => HumanId;
}
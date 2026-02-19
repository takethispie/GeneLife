using MassTransit;

namespace Genelife.Global.Messages.Events.Grocery;

public record DrinkPurchased(Guid CorrelationId, Guid GroceryStoreId, decimal Price) : CorrelatedBy<Guid>;
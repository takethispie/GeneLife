using MassTransit;

namespace Genelife.Life.Messages.Events.Grocery;

public record DrinkPurchased(Guid CorrelationId, Guid GroceryStoreId, decimal Price) : CorrelatedBy<Guid>;
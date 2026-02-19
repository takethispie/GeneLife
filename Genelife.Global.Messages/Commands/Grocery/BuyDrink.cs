using MassTransit;

namespace Genelife.Global.Messages.Commands.Grocery;

public record BuyDrink(Guid CorrelationId, Guid GroceryStoreId) : CorrelatedBy<Guid>;
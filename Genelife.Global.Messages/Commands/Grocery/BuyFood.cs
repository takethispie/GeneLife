using MassTransit;

namespace Genelife.Global.Messages.Commands.Grocery;

public record BuyFood(Guid CorrelationId, Guid GroceryStoreId) : CorrelatedBy<Guid>;
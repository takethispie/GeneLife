using MassTransit;

namespace Genelife.Messages.Events.Locomotion;

public record LeftGroceryStore( Guid CorrelationId, Guid GroceryStoreId) : CorrelatedBy<Guid>;
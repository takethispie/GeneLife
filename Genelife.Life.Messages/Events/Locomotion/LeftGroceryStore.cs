using MassTransit;

namespace Genelife.Life.Messages.Events.Locomotion;

public record LeftGroceryStore( Guid CorrelationId, Guid GroceryStoreId) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Global.Messages.Events.Locomotion;

public record LeftGroceryStore( Guid CorrelationId, Guid GroceryStoreId) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Messages.Events.Locomotion;

public record EnteredGroceryStore(Guid CorrelationId, Guid GroceryStoreId) : CorrelatedBy<Guid>;
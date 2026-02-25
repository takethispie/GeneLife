using MassTransit;

namespace Genelife.Life.Messages.Events.Locomotion;

public record EnteredGroceryStore(Guid CorrelationId, Guid GroceryStoreId) : CorrelatedBy<Guid>;
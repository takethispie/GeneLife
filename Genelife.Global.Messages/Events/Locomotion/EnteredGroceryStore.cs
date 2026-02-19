using MassTransit;

namespace Genelife.Global.Messages.Events.Locomotion;

public record EnteredGroceryStore(Guid CorrelationId, Guid GroceryStoreId) : CorrelatedBy<Guid>;
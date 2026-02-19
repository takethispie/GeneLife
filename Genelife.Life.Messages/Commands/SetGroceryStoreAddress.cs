using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetGroceryStoreAddress(
    Guid CorrelationId,
    Guid GroceryStoreId,
    float X,
    float Y,
    float Z
) : CorrelatedBy<Guid>;

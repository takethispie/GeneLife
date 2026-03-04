using MassTransit;

namespace Genelife.Messages.Commands;

public record AddGroceryStoreAddress(
    Guid CorrelationId,
    Guid GroceryStoreId,
    float X,
    float Y,
    float Z
) : CorrelatedBy<Guid>;

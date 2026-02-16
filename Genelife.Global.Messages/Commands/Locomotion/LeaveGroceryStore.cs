namespace Genelife.Global.Messages.Commands.Locomotion;

public record LeaveGroceryStore(
    Guid CorrelationId,
    Guid GroceryStoreId
);
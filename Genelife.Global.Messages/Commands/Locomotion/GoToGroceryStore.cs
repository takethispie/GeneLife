namespace Genelife.Global.Messages.Commands.Locomotion;

public record GoToGroceryStore(
    Guid CorrelationId,
    Guid GroceryStoreId
);
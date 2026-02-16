namespace Genelife.Global.Messages.Events.Locomotion;

public record EnteredGroceryStore(
    Guid HumanId,
    Guid GroceryStoreId
);
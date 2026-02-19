namespace Genelife.Global.Messages.Events.Buildings;

public record GroceryStoreAddressAnnounced(
    Guid GroceryStoreId,
    float X,
    float Y,
    float Z
);

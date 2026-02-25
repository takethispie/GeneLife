namespace Genelife.Life.Messages.Events.Buildings;

public record GroceryStoreAddressAnnounced(
    Guid GroceryStoreId,
    float X,
    float Y,
    float Z
);

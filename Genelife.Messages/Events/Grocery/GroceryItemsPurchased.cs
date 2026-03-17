namespace Genelife.Messages.Events.Grocery;

public record GroceryItemsPurchased(Guid CorrelationId, Guid StoreId, int TotalPrice, int Drinks, int Foods);
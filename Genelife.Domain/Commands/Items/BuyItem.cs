using MassTransit;

namespace Genelife.Domain.Commands;

public record GroceryListItem(ItemType ItemType, int Count);

public record BuyItems(Guid CorrelationId, GroceryListItem[] Items, Guid Buyer) : CorrelatedBy<Guid>;
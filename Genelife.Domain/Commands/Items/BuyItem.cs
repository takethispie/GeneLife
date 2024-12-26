using Genelife.Domain.Items;
using MassTransit;

namespace Genelife.Domain.Commands.Items;

public record GroceryListItem(Item Item, int Count);

public record BuyItems(Guid CorrelationId, GroceryListItem[] Items, Guid Buyer) : CorrelatedBy<Guid>;
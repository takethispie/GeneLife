using MassTransit;

namespace Genelife.Messages.Commands.Grocery;

public record BuyGroceryItems(Guid CorrelationId, Guid HumanId, int Drinks, int Foods) : CorrelatedBy<Guid>;
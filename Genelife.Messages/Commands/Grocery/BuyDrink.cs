using MassTransit;

namespace Genelife.Messages.Commands.Grocery;

public record BuyDrink(Guid CorrelationId, Guid HumanId, int Count = 1) : CorrelatedBy<Guid>;
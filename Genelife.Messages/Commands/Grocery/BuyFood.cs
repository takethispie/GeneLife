using MassTransit;

namespace Genelife.Messages.Commands.Grocery;

public record BuyFood(Guid CorrelationId, Guid HumanId, int Count = 1) : CorrelatedBy<Guid>;
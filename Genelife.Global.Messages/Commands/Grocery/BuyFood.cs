using MassTransit;

namespace Genelife.Life.Messages.Commands.Grocery;

public record BuyFood(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
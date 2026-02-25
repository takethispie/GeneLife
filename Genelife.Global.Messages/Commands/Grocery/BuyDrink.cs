using MassTransit;

namespace Genelife.Life.Messages.Commands.Grocery;

public record BuyDrink(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Domain.Commands.Survival;

public record Drink(Guid CorrelationId) : CorrelatedBy<Guid>;
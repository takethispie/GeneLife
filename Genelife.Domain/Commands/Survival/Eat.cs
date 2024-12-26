using MassTransit;

namespace Genelife.Domain.Commands.Survival;

public record Eat(Guid CorrelationId) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Domain.Commands;

public record Eat(Guid CorrelationId) : CorrelatedBy<Guid>;
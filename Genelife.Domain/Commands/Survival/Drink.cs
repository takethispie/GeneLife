using MassTransit;

namespace Genelife.Domain.Commands;

public record Drink(Guid CorrelationId) : CorrelatedBy<Guid>;
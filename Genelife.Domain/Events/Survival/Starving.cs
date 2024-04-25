using MassTransit;

namespace Genelife.Domain.Events;

public record Starving(Guid CorrelationId) : CorrelatedBy<Guid>;

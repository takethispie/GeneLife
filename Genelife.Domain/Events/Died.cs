using MassTransit;

namespace Genelife.Domain.Events;

public record Died(Guid CorrelationId) : CorrelatedBy<Guid>;

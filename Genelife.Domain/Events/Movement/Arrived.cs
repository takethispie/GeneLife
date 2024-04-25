using MassTransit;

namespace Genelife.Domain.Events;

public record Arrived(Guid CorrelationId, Guid TargetId) : CorrelatedBy<Guid>;

using MassTransit;

namespace Genelife.Domain.Events.Movement;

public record Arrived(Guid CorrelationId, Guid TargetId) : CorrelatedBy<Guid>;

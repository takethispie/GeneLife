using MassTransit;

namespace Genelife.Domain.Events;

public record StartedWorking(Guid CorrelationId) : CorrelatedBy<Guid>;
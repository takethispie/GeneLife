using MassTransit;

namespace Genelife.Domain.Events;

public record Busy(Guid CorrelationId) : CorrelatedBy<Guid>;
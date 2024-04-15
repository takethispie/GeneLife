using MassTransit;

namespace Genelife.Domain.Events;

public record HasDrank(Guid CorrelationId) : CorrelatedBy<Guid>;
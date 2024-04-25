using MassTransit;

namespace Genelife.Domain.Events;

public record ItemStored(Guid CorrelationId) : CorrelatedBy<Guid>;
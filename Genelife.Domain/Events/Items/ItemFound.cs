using MassTransit;

namespace Genelife.Domain.Events;

public record ItemFound(Guid CorrelationId, ItemType ItemType) : CorrelatedBy<Guid>;
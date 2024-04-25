using MassTransit;

namespace Genelife.Domain.Events;

public record ItemNotFound(Guid CorrelationId, ItemType ItemType) : CorrelatedBy<Guid>;
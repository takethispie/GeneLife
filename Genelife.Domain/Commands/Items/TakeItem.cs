using MassTransit;

namespace Genelife.Domain.Commands;

public record TakeItem(Guid CorrelationId, ItemType ItemType) : CorrelatedBy<Guid>; 
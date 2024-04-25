using MassTransit;

namespace Genelife.Domain.Events;

public record ItemBought(Guid CorrelationId, ItemType ItemType) : CorrelatedBy<Guid>;
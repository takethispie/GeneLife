using MassTransit;

namespace Genelife.Domain.Events;

public record ItemBought(Guid CorrelationId, Item Item, int Cost) : CorrelatedBy<Guid>;
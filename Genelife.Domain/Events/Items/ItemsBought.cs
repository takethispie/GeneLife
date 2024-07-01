using MassTransit;

namespace Genelife.Domain.Events;

public record ItemsBought(Guid CorrelationId, Item[] Items, int Cost) : CorrelatedBy<Guid>;
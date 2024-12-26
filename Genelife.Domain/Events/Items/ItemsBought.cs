using Genelife.Domain.Items;
using MassTransit;

namespace Genelife.Domain.Events.Items;

public record ItemsBought(Guid CorrelationId, Item[] Items, int Cost) : CorrelatedBy<Guid>;
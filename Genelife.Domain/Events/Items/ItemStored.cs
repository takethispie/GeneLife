using MassTransit;

namespace Genelife.Domain.Events.Items;

public record ItemStored(Guid CorrelationId) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Domain.Commands.Items;

public record StoreItem(Guid CorrelationId, int ItemId) : CorrelatedBy<Guid>;
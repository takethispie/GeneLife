using MassTransit;

namespace Genelife.Domain.Commands;

public record StoreItem(Guid CorrelationId, int ItemId) : CorrelatedBy<Guid>;
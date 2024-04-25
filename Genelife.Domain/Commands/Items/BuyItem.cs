using MassTransit;

namespace Genelife.Domain.Commands;

public record BuyItem(Guid CorrelationId, ItemType ItemType, Guid TargetStore) : CorrelatedBy<Guid>;
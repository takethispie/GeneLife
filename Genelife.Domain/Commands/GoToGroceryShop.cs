using MassTransit;

namespace Genelife.Domain.Commands;

public record GoToGroceryShop(Guid CorrelationId) : CorrelatedBy<Guid>;


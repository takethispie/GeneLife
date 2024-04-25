using MassTransit;

namespace Genelife.Domain.Events;

public record HasEaten(Guid CorrelationId) : CorrelatedBy<Guid>;
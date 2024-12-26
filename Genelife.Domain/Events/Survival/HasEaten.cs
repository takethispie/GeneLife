using MassTransit;

namespace Genelife.Domain.Events.Survival;

public record HasEaten(Guid CorrelationId) : CorrelatedBy<Guid>;
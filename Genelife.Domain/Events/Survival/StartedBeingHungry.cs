using MassTransit;

namespace Genelife.Domain.Events.Survival;

public record StartedBeingHungry(Guid CorrelationId) : CorrelatedBy<Guid>;
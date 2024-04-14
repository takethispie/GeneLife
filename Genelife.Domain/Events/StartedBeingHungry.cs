using MassTransit;

namespace Genelife.Domain.Events;

public record StartedBeingHungry(Guid CorrelationId) : CorrelatedBy<Guid>;
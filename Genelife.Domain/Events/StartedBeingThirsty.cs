using MassTransit;

namespace Genelife.Domain.Events;

public record StartedBeingThirsty(Guid CorrelationId) : CorrelatedBy<Guid>;
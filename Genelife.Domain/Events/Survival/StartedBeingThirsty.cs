using MassTransit;

namespace Genelife.Domain.Events.Survival;

public record StartedBeingThirsty(Guid CorrelationId) : CorrelatedBy<Guid>;
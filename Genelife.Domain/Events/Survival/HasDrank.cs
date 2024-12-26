using MassTransit;

namespace Genelife.Domain.Events.Survival;

public record HasDrank(Guid CorrelationId) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Domain.Events.Survival;

public record Starving(Guid CorrelationId) : CorrelatedBy<Guid>;

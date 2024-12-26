
using MassTransit;

namespace Genelife.Domain.Events.Survival;

public record Dehydrated(Guid CorrelationId) : CorrelatedBy<Guid>;

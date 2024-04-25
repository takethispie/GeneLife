
using MassTransit;

namespace Genelife.Domain.Events;

public record Dehydrated(Guid CorrelationId) : CorrelatedBy<Guid>;

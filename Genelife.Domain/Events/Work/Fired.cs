using MassTransit;

namespace Genelife.Domain.Events;

public record Fired(Guid CorrelationId, Guid Company) : CorrelatedBy<Guid>;
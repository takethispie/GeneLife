using MassTransit;

namespace Genelife.Domain.Events.Work;

public record Fired(Guid CorrelationId, Guid Company) : CorrelatedBy<Guid>;
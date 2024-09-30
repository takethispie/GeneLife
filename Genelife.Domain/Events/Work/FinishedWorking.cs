using MassTransit;

namespace Genelife.Domain.Events;

public record FinishedWorking(Guid CorrelationId) : CorrelatedBy<Guid>;
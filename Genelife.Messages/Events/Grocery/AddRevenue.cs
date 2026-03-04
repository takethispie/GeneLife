using MassTransit;

namespace Genelife.Messages.Events.Grocery;

public record AddRevenue(Guid CorrelationId, decimal Amount) : CorrelatedBy<Guid>;

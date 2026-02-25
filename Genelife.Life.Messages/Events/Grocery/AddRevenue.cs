using MassTransit;

namespace Genelife.Life.Messages.Events.Grocery;

public record AddRevenue(Guid CorrelationId, decimal Amount) : CorrelatedBy<Guid>;

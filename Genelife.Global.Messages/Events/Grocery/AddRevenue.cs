using MassTransit;

namespace Genelife.Global.Messages.Events.Grocery;

public record AddRevenue(Guid CorrelationId, decimal Amount) : CorrelatedBy<Guid>;

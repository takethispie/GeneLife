using MassTransit;

namespace Genelife.Messages.Commands;

public record AddMoney(Guid CorrelationId, float Amount) : CorrelatedBy<Guid>;
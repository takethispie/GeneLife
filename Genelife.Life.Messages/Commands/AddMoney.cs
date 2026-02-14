using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record AddMoney(Guid CorrelationId, float Amount) : CorrelatedBy<Guid>;
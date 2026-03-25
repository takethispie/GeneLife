using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record AddMoney(Guid CorrelationId, float Amount) : CorrelatedBy<Guid>;
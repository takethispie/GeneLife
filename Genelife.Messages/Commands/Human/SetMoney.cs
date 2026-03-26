using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record SetMoney(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
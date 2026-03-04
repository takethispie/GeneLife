using MassTransit;

namespace Genelife.Messages.Commands;

public record SetMoney(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
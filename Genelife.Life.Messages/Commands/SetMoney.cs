using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetMoney(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
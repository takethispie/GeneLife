using MassTransit;

namespace Genelife.Messages.Commands;

public record SetHunger(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
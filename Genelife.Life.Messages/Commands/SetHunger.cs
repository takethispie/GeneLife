using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetHunger(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
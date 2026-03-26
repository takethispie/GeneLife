using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record SetHunger(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
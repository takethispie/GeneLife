using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record SetHygiene(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Messages.Commands;

public record SetHygiene(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
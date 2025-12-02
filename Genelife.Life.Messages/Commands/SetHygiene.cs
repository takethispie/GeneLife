using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetHygiene(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
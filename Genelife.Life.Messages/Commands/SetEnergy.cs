using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetEnergy(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
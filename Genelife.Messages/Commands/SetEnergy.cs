using MassTransit;

namespace Genelife.Messages.Commands;

public record SetEnergy(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
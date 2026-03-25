using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record SetEnergy(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
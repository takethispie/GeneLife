using MassTransit;

namespace Genelife.Domain.Commands.Cheat;

public record SetEnergy(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
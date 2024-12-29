using MassTransit;

namespace Genelife.Domain.Commands.Cheat;

public record SetHunger(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
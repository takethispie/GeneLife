using MassTransit;

namespace Genelife.Domain.Commands.Cheat;

public record SetHygiene(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
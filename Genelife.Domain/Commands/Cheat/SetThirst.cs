using MassTransit;

namespace Genelife.Domain.Commands.Cheat;

public record SetThirst(Guid CorrelationId, int Value) : CorrelatedBy<Guid>;
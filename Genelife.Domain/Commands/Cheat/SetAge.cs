using MassTransit;

namespace Genelife.Domain.Commands.Cheat;

public record SetAge(Guid CorrelationId, int Value) : CorrelatedBy<Guid>;
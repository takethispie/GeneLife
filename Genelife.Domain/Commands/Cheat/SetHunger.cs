using MassTransit;

namespace Genelife.Domain.Commands.Cheat;

public record SetHunger(Guid CorrelationId, int Value) : CorrelatedBy<Guid>;
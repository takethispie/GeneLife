using MassTransit;

namespace Genelife.Domain.Commands.Cheat;

public record SetMoney(Guid CorrelationId, float Value) : CorrelatedBy<Guid>;
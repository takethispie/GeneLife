using MassTransit;

namespace Genelife.Domain.Commands;

public record SetHunger(Guid CorrelationId, int Value) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Domain.Commands;

public record SetThirst(Guid CorrelationId, int Value) : CorrelatedBy<Guid>;
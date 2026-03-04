using MassTransit;

namespace Genelife.Messages.Commands;

public record SetAge(Guid CorrelationId, int Value) : CorrelatedBy<Guid>;
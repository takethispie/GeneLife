using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetAge(Guid CorrelationId, int Value) : CorrelatedBy<Guid>;
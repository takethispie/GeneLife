using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record SetAge(Guid CorrelationId, int Value) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetHomeAddress(Guid CorrelationId, Guid HomeId) : CorrelatedBy<Guid>;
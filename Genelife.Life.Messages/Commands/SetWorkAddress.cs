using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetWorkAddress(Guid CorrelationId, Guid OfficeId) : CorrelatedBy<Guid>;
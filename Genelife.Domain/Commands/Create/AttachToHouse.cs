using MassTransit;

namespace Genelife.Domain.Commands;

public record AttachToHouse(Guid CorrelationId, Guid Being) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Domain.Commands.Create;

public record AttachToHouse(Guid CorrelationId, Guid Being) : CorrelatedBy<Guid>;
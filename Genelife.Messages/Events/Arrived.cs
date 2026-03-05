using MassTransit;

namespace Genelife.Messages.Events;

public record Arrived(Guid CorrelationId, float X, float Y, float Z, string LocationName) : CorrelatedBy<Guid>;
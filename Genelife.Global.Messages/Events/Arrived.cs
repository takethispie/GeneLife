using MassTransit;

namespace Genelife.Global.Messages.Events;

public record Arrived(Guid CorrelationId, float X, float Y, float Z, string LocationName) : CorrelatedBy<Guid>;
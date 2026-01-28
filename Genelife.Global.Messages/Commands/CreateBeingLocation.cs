using MassTransit;

namespace Genelife.Global.Messages.Commands;

public record CreateBeingLocation(Guid CorrelationId, Guid HumanId, float X, float Y, float Z) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Global.Messages.Events.Locomotion;

public record LeftWork(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
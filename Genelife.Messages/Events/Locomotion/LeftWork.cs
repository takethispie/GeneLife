using MassTransit;

namespace Genelife.Messages.Events.Locomotion;

public record LeftWork(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
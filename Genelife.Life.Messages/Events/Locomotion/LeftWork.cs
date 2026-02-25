using MassTransit;

namespace Genelife.Life.Messages.Events.Locomotion;

public record LeftWork(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
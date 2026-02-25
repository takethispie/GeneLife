using MassTransit;

namespace Genelife.Life.Messages.Events.Locomotion;

public record EnteredWork(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
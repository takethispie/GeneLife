using MassTransit;

namespace Genelife.Messages.Events.Locomotion;

public record EnteredWork(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
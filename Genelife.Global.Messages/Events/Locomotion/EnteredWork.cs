using MassTransit;

namespace Genelife.Global.Messages.Events.Locomotion;

public record EnteredWork(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
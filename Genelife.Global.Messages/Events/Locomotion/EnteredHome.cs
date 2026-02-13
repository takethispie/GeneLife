using MassTransit;

namespace Genelife.Global.Messages.Events.Locomotion;

public record EnteredHome(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
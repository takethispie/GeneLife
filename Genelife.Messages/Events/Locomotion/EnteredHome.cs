using MassTransit;

namespace Genelife.Messages.Events.Locomotion;

public record EnteredHome(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
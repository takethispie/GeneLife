using MassTransit;

namespace Genelife.Life.Messages.Events.Locomotion;

public record EnteredHome(Guid CorrelationId, Guid BeingId) : CorrelatedBy<Guid>;
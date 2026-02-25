using MassTransit;

namespace Genelife.Life.Messages.Events.Locomotion;

public record LeftHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
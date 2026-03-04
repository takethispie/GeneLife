using MassTransit;

namespace Genelife.Messages.Events.Locomotion;

public record LeftHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
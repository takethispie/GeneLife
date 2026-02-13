using MassTransit;

namespace Genelife.Global.Messages.Events.Locomotion;

public record LeftHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
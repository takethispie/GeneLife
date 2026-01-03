using MassTransit;

namespace Genelife.Global.Messages.Commands.Locomotion;

public record GoHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
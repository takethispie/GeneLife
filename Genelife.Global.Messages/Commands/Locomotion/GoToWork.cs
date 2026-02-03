using MassTransit;

namespace Genelife.Global.Messages.Commands.Locomotion;

public record GoToWork(Guid CorrelationId) : CorrelatedBy<Guid>;
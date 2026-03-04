using MassTransit;

namespace Genelife.Messages.Commands.Locomotion;

public record GoHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
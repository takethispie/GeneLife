using MassTransit;

namespace Genelife.Life.Messages.Commands.Locomotion;

public record GoHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
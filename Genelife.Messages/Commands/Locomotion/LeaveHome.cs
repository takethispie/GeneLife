using MassTransit;

namespace Genelife.Messages.Commands.Locomotion;

public record LeaveHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
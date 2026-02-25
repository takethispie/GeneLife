using MassTransit;

namespace Genelife.Life.Messages.Commands.Locomotion;

public record LeaveHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
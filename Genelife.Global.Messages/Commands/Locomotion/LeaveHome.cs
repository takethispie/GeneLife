using MassTransit;

namespace Genelife.Global.Messages.Commands.Locomotion;

public record LeaveHome(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;
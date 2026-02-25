using MassTransit;

namespace Genelife.Life.Messages.Commands.Locomotion;

public record LeaveGroceryStore(
    Guid CorrelationId,
    Guid HumanId
) : CorrelatedBy<Guid>;
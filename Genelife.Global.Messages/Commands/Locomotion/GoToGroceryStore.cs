using MassTransit;

namespace Genelife.Life.Messages.Commands.Locomotion;

public record GoToGroceryStore(
    Guid CorrelationId,
    Guid HumanId
) : CorrelatedBy<Guid>;

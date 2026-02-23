using MassTransit;

namespace Genelife.Global.Messages.Commands.Locomotion;

public record GoToGroceryStore(
    Guid CorrelationId,
    Guid HumanId
) : CorrelatedBy<Guid>;

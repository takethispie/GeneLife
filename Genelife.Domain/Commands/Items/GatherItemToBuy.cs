using MassTransit;

namespace Genelife.Domain.Commands;

public record ListFoodAndDrinkToBuy(Guid CorrelationId, Guid TargetGroceryShop) : CorrelatedBy<Guid>;
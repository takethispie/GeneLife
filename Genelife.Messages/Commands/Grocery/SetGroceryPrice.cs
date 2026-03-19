using MassTransit;

namespace Genelife.Messages.Commands.Grocery;

public record SetGroceryPrice(
    Guid CorrelationId,
    int? FoodPrice,
    int? DrinkPrice
) : CorrelatedBy<Guid>;

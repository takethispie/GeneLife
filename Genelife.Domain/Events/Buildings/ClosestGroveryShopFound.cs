using MassTransit;

namespace Genelife.Domain.Events.Buildings;

public sealed record ClosestGroceryShopFound(Guid CorrelationId, float X, float Y, float Z, Guid GroceryShopId): CorrelatedBy<Guid>;
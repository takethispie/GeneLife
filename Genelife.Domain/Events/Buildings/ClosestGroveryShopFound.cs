using System.Numerics;
using MassTransit;

namespace Genelife.Domain.Events;

public sealed record ClosestGroceryShopFound(Guid CorrelationId, Vector3 Position, Guid GroceryShopId): CorrelatedBy<Guid>;
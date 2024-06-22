using System.Numerics;
using MassTransit;

namespace Genelife.Domain.Commands;

public sealed record FindClosestGroceryShop(Guid CorrelationId, Vector3 sourcePosition) : CorrelatedBy<Guid>;
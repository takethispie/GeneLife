using System.Numerics;
using MassTransit;

namespace Genelife.Domain.Commands.Buildings;

public sealed record FindClosestGroceryShop(Guid CorrelationId, Vector3 SourcePosition) : CorrelatedBy<Guid>;
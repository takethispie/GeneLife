using System.Numerics;
using MassTransit;

namespace Genelife.Domain.Commands;

public record CreateGroceryShop(Guid CorrelationId, Vector3 Position, Vector2 Size) : CorrelatedBy<Guid>;
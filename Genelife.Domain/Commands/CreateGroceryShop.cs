using System.Numerics;
using MassTransit;

namespace Genelife.Domain.Commands;

public record CreateGroceryShop(Guid CorrelationId, int X, int Y, Vector2 Size) : CorrelatedBy<Guid>;
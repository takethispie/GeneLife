using System.Numerics;
using MassTransit;

namespace Genelife.Domain.Commands;

public record CreateGroceryShop(Guid CorrelationId, int X, int Y, int Width, int Depth) : CorrelatedBy<Guid>;
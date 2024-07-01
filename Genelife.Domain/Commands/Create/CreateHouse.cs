using MassTransit;

namespace Genelife.Domain.Commands;

public record CreateHouse(Guid CorrelationId, int X, int Y, int Width, int Depth) : CorrelatedBy<Guid>;
using MassTransit;

namespace Genelife.Domain.Commands.Create;

public record CreateHouse(Guid CorrelationId, int X, int Y, int Width, int Depth) : CorrelatedBy<Guid>;
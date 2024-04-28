using MassTransit;
using System.Numerics;

namespace Genelife.Domain.Commands;

public record CreateHuman(Guid CorrelationId, Human Human, int X, int Y) : CorrelatedBy<Guid>;
using MassTransit;
using System.Numerics;

namespace Genelife.Domain.Commands;

public record CreateHuman(Guid CorrelationId, Human Human, Vector3 Position) : CorrelatedBy<Guid>;
using MassTransit;
using System.Numerics;

namespace Genelife.Domain.Commands;

public record MoveTo(Guid CorrelationId, Vector3 Position) : CorrelatedBy<Guid>;

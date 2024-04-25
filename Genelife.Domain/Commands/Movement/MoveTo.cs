using MassTransit;
using System.Numerics;

namespace Genelife.Domain.Commands;

public record MoveTo(Guid CorrelationId, int X, int Y, Guid TargetId) : CorrelatedBy<Guid>;

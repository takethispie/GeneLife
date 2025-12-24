using System.Numerics;
using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record AddHomeAddress(Guid CorrelationId, Vector3 Location) : CorrelatedBy<Guid>;
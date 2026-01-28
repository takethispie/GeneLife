using System.Numerics;
using MassTransit;

namespace Genelife.Global.Messages.Commands;

public record AddWorkAddress(Guid CorrelationId, Vector3 Location, Guid OfficeId) : CorrelatedBy<Guid>;
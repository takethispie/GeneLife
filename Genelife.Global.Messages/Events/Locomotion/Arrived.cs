using System.Numerics;
using MassTransit;

namespace Genelife.Global.Messages.Events.Locomotion;

public record Arrived(Guid CorrelationId, Vector3 Location, string LocationName) : CorrelatedBy<Guid>;
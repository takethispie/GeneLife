using System.Numerics;
using MassTransit;

namespace Genelife.Global.Messages.Events.Locomotion;

public record Arrived(Guid CorrelationId, float X, float Y, float Z, string LocationName) : CorrelatedBy<Guid>;
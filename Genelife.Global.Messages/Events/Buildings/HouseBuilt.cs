using System.Numerics;
using MassTransit;

namespace Genelife.Global.Messages.Events.Buildings;

public record HouseBuilt(Guid CorrelationId, Vector3 Location, List<Guid>? Owners = null) : CorrelatedBy<Guid>;
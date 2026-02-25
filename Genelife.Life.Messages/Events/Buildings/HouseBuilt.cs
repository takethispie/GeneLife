using System.Numerics;
using MassTransit;

namespace Genelife.Life.Messages.Events.Buildings
{
    public record HouseBuilt(Guid CorrelationId, float X, float Y, float Z, List<Guid>? Owners = null) : CorrelatedBy<Guid>;
}
using System.Numerics;
using MassTransit;

namespace Genelife.Global.Messages.Events.Buildings;

public record OfficeCreated(Guid CorrelationId, Vector3 Location, string Name, Guid OwningCompanyId) : CorrelatedBy<Guid>;
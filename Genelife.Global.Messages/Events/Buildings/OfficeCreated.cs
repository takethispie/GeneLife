using System.Numerics;
using MassTransit;

namespace Genelife.Global.Messages.Events.Buildings;

public record OfficeCreated(Guid CorrelationId, float X, float Y, float Z, string Name, Guid OwningCompanyId) : CorrelatedBy<Guid>;
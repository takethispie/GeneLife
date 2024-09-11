using System.Numerics;
using MassTransit;

namespace Genelife.Domain.Commands;

public record CreateCompany(Guid CorrelationId, string Name, Vector3 HQPosition) : CorrelatedBy<Guid>;
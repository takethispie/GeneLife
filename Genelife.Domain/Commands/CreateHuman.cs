using Genelife.Domain;
using MassTransit;

namespace Genelife.Domain.Commands;

public record CreateHuman(Guid CorrelationId, Human Human) : CorrelatedBy<Guid>;
using Genelife.Domain;
using MassTransit;

namespace Genelife.Messages.Commands;

public record CreateHuman(Guid CorrelationId, Human Human) : CorrelatedBy<Guid>;
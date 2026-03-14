using Genelife.Domain;
using Genelife.Domain.Human;
using MassTransit;

namespace Genelife.Messages.Commands;

public record CreateHuman(Guid CorrelationId, Person Person) : CorrelatedBy<Guid>;
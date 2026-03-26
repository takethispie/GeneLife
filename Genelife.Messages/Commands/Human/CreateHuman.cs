using Genelife.Domain.Human;
using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record CreateHuman(Guid CorrelationId, Person Person) : CorrelatedBy<Guid>;
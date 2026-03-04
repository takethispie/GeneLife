using Genelife.Domain.Skills;
using MassTransit;

namespace Genelife.Messages.Commands.Worker;

public record CreateWorker(
    Guid CorrelationId, 
    Guid HumanId, 
    string Firstname, 
    string Lastname, 
    SkillSet SkillSet
) : CorrelatedBy<Guid>;
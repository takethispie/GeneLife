using Genelife.Work.Messages.DTOs.Skills;
using MassTransit;

namespace Genelife.Work.Messages.Commands.Worker;

public record CreateWorker(
    Guid CorrelationId, 
    Guid HumanId, 
    string Firstname, 
    string Lastname, 
    SkillSet SkillSet
) : CorrelatedBy<Guid>;
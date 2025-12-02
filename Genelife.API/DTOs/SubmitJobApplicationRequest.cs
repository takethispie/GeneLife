
using Genelife.Work.Messages.DTOs.Skills;

namespace Genelife.API.DTOs;

public record SubmitJobApplicationRequest(
    SkillSet SkillSet,
    Guid JobPostingId,
    Guid HumanId,
    int Experience,
    float RequestedSalary
);
using Genelife.Domain.Work.Skills;

namespace Genelife.API.DTOs;

public record SubmitJobApplicationRequest(
    SkillSet SkillSet,
    Guid JobPostingId,
    Guid HumanId,
    int Experience,
    float RequestedSalary
);
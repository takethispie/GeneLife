using Genelife.Domain.Work.Skills;

namespace Genelife.Domain.Work;

public record JobApplication(
    Guid JobPostingId,
    Guid HumanId,
    DateTime ApplicationDate,
    float RequestedSalary,
    SkillSet Skills,
    int YearsOfExperience,
    float MatchScore = 0.0f
);
using Genelife.Work.Messages.DTOs.Skills;

namespace Genelife.Work.Messages.DTOs;

public record JobApplication(
    Guid JobPostingId,
    Guid HumanId,
    DateTime ApplicationDate,
    float RequestedSalary,
    SkillSet Skills,
    int YearsOfExperience,
    float MatchScore = 0.0f
);
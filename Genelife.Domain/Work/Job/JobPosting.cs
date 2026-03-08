using Genelife.Domain.Work.Skills;

namespace Genelife.Domain.Work.Job;

public record JobPosting(
    Guid CompanyId,
    string Title,
    float SalaryMin,
    float SalaryMax,
    CompanyType CompanyType,
    JobLevel Level,
    SkillSet RequiredSkillSet,
    Position OfficeLocation,
    int MaxApplications = 50
);
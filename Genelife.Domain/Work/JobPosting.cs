using Genelife.Domain.Work.Skills;

namespace Genelife.Domain.Work;

public record JobPosting(
    Guid CompanyId,
    string Title,
    float SalaryMin,
    float SalaryMax,
    CompanyType CompanyType,
    JobLevel Level,
    SkillSet RequiredSkillSet,
    OfficeLocation OfficeLocation,
    int MaxApplications = 50
);
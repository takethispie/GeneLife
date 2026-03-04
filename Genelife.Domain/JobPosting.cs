using Genelife.Domain.Skills;

namespace Genelife.Domain;

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
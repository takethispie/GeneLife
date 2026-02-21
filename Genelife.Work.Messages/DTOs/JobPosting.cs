using Genelife.Work.Messages.DTOs.Skills;

namespace Genelife.Work.Messages.DTOs;

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
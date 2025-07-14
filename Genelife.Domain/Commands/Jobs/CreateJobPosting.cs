namespace Genelife.Domain.Commands.Jobs;

public record CreateJobPosting(
    Guid CompanyId,
    string Title,
    string Description,
    List<string> Requirements,
    decimal SalaryMin,
    decimal SalaryMax,
    JobLevel Level,
    int MaxApplications = 100,
    int DaysToExpire = 30
);
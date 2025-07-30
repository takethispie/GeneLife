namespace Genelife.Domain.Work;

public record JobPosting(
    Guid CompanyId,
    string Title,
    string Description,
    List<string> Requirements,
    decimal SalaryMin,
    decimal SalaryMax,
    JobLevel Level,
    CompanyType Industry,
    DateTime PostedDate,
    DateTime? ExpiryDate,
    JobPostingStatus Status,
    int MaxApplications = 100
);
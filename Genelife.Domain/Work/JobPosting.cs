namespace Genelife.Domain;

public record JobPosting(
    string CompanyId,
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
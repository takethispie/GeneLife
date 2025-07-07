namespace Genelife.Domain;

public record JobPosting(
    Guid Id,
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

public enum JobLevel
{
    Entry,
    Junior,
    Mid,
    Senior,
    Lead,
    Manager,
    Director,
    Executive
}

public enum JobPostingStatus
{
    Draft,
    Active,
    Paused,
    Filled,
    Expired,
    Cancelled
}
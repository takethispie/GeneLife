using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Job;

namespace Genelife.API.DTOs;

// Request DTOs
public record CreateJobPostingRequest(
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
namespace Genelife.Application.IntegrationEvents;

public record JobPostingSnapshot(
    Guid PostingId,
    string Title,
    float SalaryMin,
    float SalaryMax,
    string Level,
    string CompanyType,
    int ApplicationCount,
    string CurrentState
);

public record EmployeeSnapshot(
    Guid EmployeeId,
    float Salary,
    DateTime HireDate,
    string Status,
    float ProductivityScore
);

public record CompanyUpdate(
    Guid CorrelationId,
    string Name,
    string CompanyType,
    float Revenue,
    float AverageProductivity,
    int EmployeeCount,
    int? PublishedJobPostings,
    float OfficeX,
    float OfficeY,
    float OfficeZ,
    IReadOnlyList<EmployeeSnapshot> Employees,
    IReadOnlyList<JobPostingSnapshot> ActiveJobPostings
)
{
    public static CompanyUpdate FromSaga(
        Guid correlationId,
        Domain.Work.Company company,
        int? publishedJobPostings,
        float officeX,
        float officeY,
        float officeZ,
        IReadOnlyList<JobPostingSnapshot>? activeJobPostings = null) =>
        new(
            correlationId,
            company.Name,
            company.Type.ToString(),
            company.Accounting.Revenue,
            company.AverageProductivity,
            company.Employees.Count,
            publishedJobPostings,
            officeX,
            officeY,
            officeZ,
            company.Employees.Select(e => new EmployeeSnapshot(
                e.Id,
                e.Salary,
                e.HireDate,
                e.Status.ToString(),
                e.ProductivityScore
            )).ToList(),
            activeJobPostings ?? []
        );
}

namespace Genelife.AdminFrontend.Models;

public record JobPostingSnapshotDto(
    Guid PostingId,
    string Title,
    float SalaryMin,
    float SalaryMax,
    string Level,
    string CompanyType,
    int ApplicationCount,
    string CurrentState
);

public record EmployeeSnapshotDto(
    Guid EmployeeId,
    float Salary,
    DateTime HireDate,
    string Status,
    float ProductivityScore
);

public record CompanyUpdateDto(
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
    IReadOnlyList<EmployeeSnapshotDto> Employees,
    IReadOnlyList<JobPostingSnapshotDto> ActiveJobPostings
);

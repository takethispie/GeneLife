namespace Genelife.Domain;

public record Employment(
    Guid HumanId,
    Guid CompanyId,
    decimal Salary,
    DateTime HireDate,
    EmploymentStatus Status,
    decimal ProductivityScore = 1.0m
);

public enum EmploymentStatus
{
    Active,
    OnLeave,
    Terminated,
    Retired
}

public enum PayrollState
{
    Idle,
    Processing,
    Completed
}

public enum HiringState
{
    NotHiring,
    Evaluating,
    ActivelyHiring,
    HiringComplete
}

public enum WorkProgressState
{
    Monitoring,
    Evaluating,
    Updated
}
namespace Genelife.Domain.Work.Accounting;

public record Salary(Guid EmployeeId, float Amount, float TaxDeducted);
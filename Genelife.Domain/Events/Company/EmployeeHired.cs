namespace Genelife.Domain.Events.Company;

public record EmployeeHired(Guid CompanyId, Guid HumanId, decimal Salary);
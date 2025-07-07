namespace Genelife.Domain.Events.Company;

public record HireEmployee(Guid CompanyId, Guid HumanId, decimal Salary);
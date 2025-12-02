namespace Genelife.Work.Messages.Events.Company;

public record EmployeeHired(Guid CompanyId, Guid HumanId, float Salary);
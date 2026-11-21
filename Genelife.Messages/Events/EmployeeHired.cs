namespace Genelife.Messages.Events;

public record EmployeeHired(ulong CompanyId, ulong SimId, string Position, float Salary);
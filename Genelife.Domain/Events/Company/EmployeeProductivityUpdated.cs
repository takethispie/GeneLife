namespace Genelife.Domain.Events.Company;

public record EmployeeProductivityUpdated(Guid CompanyId, Guid HumanId, float ProductivityScore);
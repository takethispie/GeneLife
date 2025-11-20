namespace Genelife.Work.Messages.Events.Company;

public record EmployeeProductivityUpdated(Guid CompanyId, Guid HumanId, float ProductivityScore);
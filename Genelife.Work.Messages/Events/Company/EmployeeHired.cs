using Genelife.Work.Messages.DTOs;

namespace Genelife.Work.Messages.Events.Company;

public record EmployeeHired(Guid CompanyId, Guid WorkerId, float Salary, Guid OfficeId, OfficeLocation OfficeLocation);
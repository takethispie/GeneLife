using Genelife.Work.Messages.DTOs;
using MassTransit;

namespace Genelife.Work.Messages.Events.Company;

public record EmployeeHired(
    Guid CorrelationId, 
    Guid WorkerId, 
    float Salary, 
    OfficeLocation OfficeLocation
) : CorrelatedBy<Guid>;
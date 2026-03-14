using Genelife.Domain;
using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Messages.Events.Company;

public record EmployeeHired(
    Guid CorrelationId, 
    Guid WorkerId, 
    float Salary, 
    Position OfficeLocation
) : CorrelatedBy<Guid>;
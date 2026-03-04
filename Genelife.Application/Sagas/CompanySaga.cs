using System.Linq.Expressions;
using Genelife.Application.Usecases;
using Genelife.Domain;
using Genelife.Messages.Commands.Company;
using Genelife.Messages.Events;
using Genelife.Messages.Events.Clock;
using Genelife.Messages.Events.Company;
using Genelife.Messages.Events.Jobs;
using Genelife.Messages.Events.Locomotion;
using MassTransit;
using Serilog;

namespace Genelife.Application.Sagas;

public class CompanySaga :
    ISaga,
    ISagaVersion,
    InitiatedBy<CreateCompany>,
    Orchestrates<EmployeeHired>,
    Orchestrates<JobPostingExpired>,
    Orchestrates<EnteredWork>,
    Orchestrates<LeftWork>,
    Observes<DayElapsed, CompanySaga>
{
    public Guid CorrelationId { get; set; }
    public Company Company { get; set; } = null!;
    public List<Employee> Employees { get; set; } = [];
    public int DaysElapsedCount { get; set; }
    public int? PublishedJobPostings { get; set; }
    public int Version { get; set; }
    public DateTime LastPayrollDate { get; set; }
    public List<Guid> Occupants { get; set; } = [];
    public OfficeLocation OfficeLocation { get; set; } = new(0, 0, 0);

    public Expression<Func<CompanySaga, DayElapsed, bool>> CorrelationExpression => (_,_) => true;

    public async Task Consume(ConsumeContext<CreateCompany> context)
    {
        Company = context.Message.Company;
        DaysElapsedCount = 0;
        LastPayrollDate = DateTime.UtcNow;
        OfficeLocation = new OfficeLocation(context.Message.X, context.Message.Y, context.Message.Z);
        Log.Information("Company {CompanyName} created with ID {SagaCorrelationId}", Company.Name, CorrelationId);
        await Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<EmployeeHired> context)
    {
        var employment = new Employee(
            context.Message.WorkerId,
            context.Message.Salary,
            DateTime.UtcNow,
            EmploymentStatus.Active
        );
        Employees.Add(employment);
        Company = Company with {
            EmployeeIds = Company.EmployeeIds.Append(context.Message.WorkerId).ToList()
        };
        PublishedJobPostings--;
        Log.Information("Company {CompanyName}: Hired employee {MessageWorkerId} with salary {MessageSalary:C}", Company.Name, context.Message.WorkerId, context.Message.Salary);
        await Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<JobPostingExpired> context)
    {
        PublishedJobPostings--;
        await Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<EnteredWork> context)
    {
        Log.Information("Human {MessageBeingId} is at {SagaCorrelationId} office", context.Message.BeingId, CorrelationId);
        Occupants = Occupants.Exists(occupant => occupant == context.Message.BeingId)
            ? Occupants
            : [..Occupants, context.Message.BeingId];
        var pos = OfficeLocation;
        await context.Publish(new Arrived(context.Message.BeingId, pos.X, pos.Y, pos.Z, "Work"));
    }

    public async Task Consume(ConsumeContext<LeftWork> context)
    {
        Occupants = Occupants.Exists(occupant => occupant == context.Message.BeingId)
            ? Occupants
            : [..Occupants, context.Message.BeingId];
        Log.Information("Human {MessageBeingId} is leaving {SagaCorrelationId} office", context.Message.BeingId, CorrelationId);
        await Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<DayElapsed> context)
    {
        DaysElapsedCount++;
        if (DaysElapsedCount >= 30) {
            Log.Information("Company {CompanyName}: Processing payroll", Company.Name);
            var (totalPaid, totalTaxes, salaryPayments) = new CalculatePayroll().Execute(Company, Employees);
            var totalPayrollCost = totalPaid + totalTaxes;
            Company = Company with { Revenue = Company.Revenue - totalPayrollCost };
            foreach (var salaryPayment in salaryPayments)
                await context.Publish(salaryPayment);
            await context.Publish(new PayrollCompleted(CorrelationId, totalPaid, totalTaxes));
            LastPayrollDate = DateTime.UtcNow;
            Log.Information("Company {CompanyName}: Payroll completed. Total paid: {TotalPaid:C}, Taxes: {TotalTaxes:C}", Company.Name, totalPaid, totalTaxes);
            DaysElapsedCount = 0;
        }

        Employees = Employees.Select(employee => new UpdateEmployeeProductivity().Execute(employee)).ToList();

        var (averageProductivity, revenueChange) = new UpdateCompanyProductivity().Execute(Company, Employees);
        Company = Company with {
            Revenue = Company.Revenue + revenueChange,
            AverageProductivity = averageProductivity
        };
        Log.Information("Company {CompanyName}: Productivity {AverageProductivity:F2}, Revenue change {RevenueChange:C}", Company.Name, averageProductivity, revenueChange);

        var postings =
            new CreateJobPostingList().Execute(
                Company,
                PublishedJobPostings,
                CorrelationId,
                OfficeLocation
            );
        foreach (var posting in postings)
            await context.Publish(posting);
        if (postings.Count > 0) PublishedJobPostings = postings.Count;
    }
}

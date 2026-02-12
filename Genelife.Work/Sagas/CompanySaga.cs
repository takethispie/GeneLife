using Genelife.Global.Messages.Commands;
using Genelife.Global.Messages.Events;
using Genelife.Global.Messages.Events.Clock;
using Genelife.Global.Messages.Events.Locomotion;
using Genelife.Work.Messages.Commands.Company;
using Genelife.Work.Messages.DTOs;
using Genelife.Work.Messages.Events.Company;
using Genelife.Work.Messages.Events.Jobs;
using Genelife.Work.Sagas.States;
using Genelife.Work.Usecases;
using MassTransit;
using Serilog;

namespace Genelife.Work.Sagas;

public class CompanySaga : MassTransitStateMachine<CompanySagaState>
{
    public State Active { get; set; } = null!;

    public Event<CreateCompany> Created { get; set; } = null!;
    public Event<DayElapsed> DayElapsed { get; set; } = null!;
    public Event<EmployeeHired> EmployeeHired { get; set; } = null!;
    public Event<JobPostingExpired> JobPostingExpired { get; set; } = null!;
    public Event<EnteredWork> HumanEntered { get; set; } = null!;
    public Event<LeftWork> HumanLeft { get; set; } = null!;

    public CompanySaga()
    {
        InstanceState(x => x.CurrentState);

        Initially(
        When(Created)
            .Then(context => {
                context.Saga.Company = context.Message.Company;
                context.Saga.DaysElapsedCount = 0;
                context.Saga.LastPayrollDate = DateTime.UtcNow;
                Log.Information($"Company {context.Saga.Company.Name} created with ID {context.Saga.CorrelationId}");
            })
            .TransitionTo(Active)
        );
        
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => EmployeeHired, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CompanyId));
        Event(() => JobPostingExpired, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CompanyId));
        Event(() => HumanEntered,
            e => e.CorrelateById(
                saga => saga.CorrelationId, 
                ctx => ctx.Message.CorrelationId
            )
        );
        
        Event(() => HumanLeft,
            e => e.CorrelateById(
                saga => saga.CorrelationId, 
                ctx => ctx.Message.CorrelationId
            )
        );
        
        DuringAny(
            When(EmployeeHired) .Then(context => {
                    var employment = new Employee(
                        context.Message.WorkerId,
                        context.Message.Salary,
                        DateTime.UtcNow,
                        EmploymentStatus.Active
                    );
                    context.Saga.Employees.Add(employment);
                    context.Saga.Company = context.Saga.Company with {
                        EmployeeIds = context.Saga.Company.EmployeeIds.Append(context.Message.WorkerId).ToList()
                    };
                    context.Saga.PublishedJobPostings--;
                    Log.Information($"Company {context.Saga.Company.Name}: Hired employee {context.Message.WorkerId} with salary {context.Message.Salary:C}");
            }),
            When(HumanEntered).Then(bc => {
                Log.Information($"Human {bc.Message.BeingId} is at {bc.Saga.CorrelationId} office");
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.BeingId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.BeingId];
                var pos = bc.Saga.Location;
                bc.Publish(new Arrived(bc.Message.BeingId,  pos.X, pos.Y, pos.Z, "Work"));
            }),
            When(HumanLeft).Then(bc => {
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.BeingId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.BeingId];
                Log.Information($"Human {bc.Message.BeingId} is leaving {bc.Saga.CorrelationId} office");
            }),
            When(JobPostingExpired) .Then(bc => bc.Saga.PublishedJobPostings--)
        );
        
        During(Active,
            When(DayElapsed) .Then(context => {
                    context.Saga.DaysElapsedCount++;
                    if (context.Saga.DaysElapsedCount >= 30) {
                        Log.Information($"Company {context.Saga.Company.Name}: Processing payroll");
                        var (totalPaid, totalTaxes, salaryPayments) = new CalculatePayroll().Execute(context.Saga.Company, context.Saga.Employees);
                        var totalPayrollCost = totalPaid + totalTaxes;
                        context.Saga.Company = context.Saga.Company with { Revenue = context.Saga.Company.Revenue - totalPayrollCost };
                        salaryPayments.ForEach(salaryPayment => context.Publish(salaryPayment));
                        context.Publish(new PayrollCompleted(context.Saga.CorrelationId, totalPaid, totalTaxes));
                        context.Saga.LastPayrollDate = DateTime.UtcNow;
                        Log.Information($"Company {context.Saga.Company.Name}: Payroll completed. Total paid: {totalPaid:C}, Taxes: {totalTaxes:C}");
                        context.Saga.DaysElapsedCount = 0;
                    }

                    context.Saga.Employees = context.Saga.Employees.Select(employee => 
                        new UpdateEmployeeProductivity().Execute(employee)
                    ).ToList();

                    var (averageProductivity, revenueChange) = new UpdateCompanyProductivity().Execute(context.Saga.Company, context.Saga.Employees);
                    context.Saga.Company = context.Saga.Company with {
                        Revenue = context.Saga.Company.Revenue + revenueChange,
                        AverageProductivity = averageProductivity
                    };
                    Log.Information($"Company {context.Saga.Company.Name}: Productivity {averageProductivity:F2}, Revenue change {revenueChange:C}");
                    var postings = 
                        new CreateJobPostingList().Execute(
                            context.Saga.Company, 
                            context.Saga.PublishedJobPostings, 
                            context.Saga.CorrelationId, 
                            context.Saga.OfficeId,
                            context.Saga.OfficeLocation
                        );
                    postings.ForEach(posting => context.Publish(posting));
                    if(postings.Count > 0) context.Saga.PublishedJobPostings = postings.Count;
            })
        );
    }
}
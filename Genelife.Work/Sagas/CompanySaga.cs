using Genelife.Global.Messages.Events.Clock;
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
        Event(() => EmployeeHired, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CompanyId.ToString()));
        Event(() => JobPostingExpired, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CompanyId.ToString()));
        
        DuringAny(
            When(EmployeeHired) .Then(context => {
                    var employment = new Employee(
                        context.Message.HumanId,
                        context.Message.Salary,
                        DateTime.UtcNow,
                        EmploymentStatus.Active
                    );
                    context.Saga.Employees.Add(employment);
                    context.Saga.Company = context.Saga.Company with {
                        EmployeeIds = context.Saga.Company.EmployeeIds.Append(context.Message.HumanId).ToList()
                    };
                    context.Saga.PublishedJobPostings--;
                    Log.Information($"Company {context.Saga.Company.Name}: Hired employee {context.Message.HumanId} with salary {context.Message.Salary:C}");
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
                        new CreateJobPostingList().Execute(context.Saga.Company, context.Saga.PublishedJobPostings, context.Saga.CorrelationId);
                    postings.ForEach(posting => context.Publish(posting));
                    if(postings.Count > 0) context.Saga.PublishedJobPostings = postings.Count;
            })
        );
    }
}
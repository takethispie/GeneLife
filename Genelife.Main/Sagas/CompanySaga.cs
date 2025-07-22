using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Commands.Company;
using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Work;
using Genelife.Main.Usecases;
using MassTransit;
using Serilog;

namespace Genelife.Main.Sagas;

public class CompanySaga : MassTransitStateMachine<CompanySagaState>
{
    public State Active { get; set; } = null!;
    public State Payroll { get; set; } = null!;
    public State Hiring { get; set; } = null!;
    public State WorkProgress { get; set; } = null!;

    public Event<CreateCompany> Created { get; set; } = null!;
    public Event<DayElapsed> DayElapsed { get; set; } = null!;
    public Event<EmployeeHired> EmployeeHired { get; set; } = null!;
    public Event<EmployeeProductivityUpdated> ProductivityUpdated { get; set; } = null!;
    public Event<StartHiring> StartHiring { get; set; } = null!;
    public Event<ProcessPayroll> ProcessPayroll { get; set; } = null!;
    public Event<UpdateWorkProgress> UpdateWorkProgress { get; set; } = null!;
    
    private readonly Random random = new();

    public CompanySaga()
    {
        InstanceState(x => x.CurrentState);

        Initially(
            When(Created)
                .Then(context =>
                {
                    context.Saga.Company = context.Message.Company;
                    context.Saga.DaysElapsedCount = 0;
                    context.Saga.PayrollState = PayrollState.Idle;
                    context.Saga.HiringState = HiringState.NotHiring;
                    context.Saga.WorkProgressState = WorkProgressState.Monitoring;
                    context.Saga.LastPayrollDate = DateTime.UtcNow;
                    context.Saga.AverageProductivity = 1.0m;
                    Log.Information($"Company {context.Saga.Company.Name} created with ID {context.Saga.CorrelationId}");
                })
                .TransitionTo(Active)
        );

        // Configure event correlations
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => EmployeeHired, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CompanyId.ToString()));
        Event(() => ProductivityUpdated, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CompanyId.ToString()));
        Event(() => StartHiring, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CompanyId.ToString()));
        Event(() => ProcessPayroll, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CompanyId.ToString()));
        Event(() => UpdateWorkProgress, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CompanyId.ToString()));

        During(Active,
            When(DayElapsed)
                .Then(context =>
                {
                    context.Saga.DaysElapsedCount++;
                    Log.Information($"Company {context.Saga.Company.Name}: Day {context.Saga.DaysElapsedCount} elapsed");

                    // Check if payroll is due (every 30 days)
                    if (context.Saga.DaysElapsedCount >= 30)
                    {
                        Log.Information($"Company {context.Saga.Company.Name}: Payroll due, transitioning to Payroll state");
                        context.Saga.PayrollState = PayrollState.Processing;
                        context.TransitionToState(Payroll);
                        return;
                    }

                    // Daily work progress update
                    var (averageProductivity, revenueChange) = new UpdateProductivity().Execute(context.Saga.Company, context.Saga.Employees);
                    context.Saga.AverageProductivity = averageProductivity;

                    // Update company revenue
                    var updatedCompany = context.Saga.Company with { Revenue = context.Saga.Company.Revenue + revenueChange };
                    context.Saga.Company = updatedCompany;
                    Log.Information($"Company {context.Saga.Company.Name}: Productivity {averageProductivity:F2}, Revenue change {revenueChange:C}");

                    // Evaluate hiring needs
                    var (shouldHire, positionsNeeded) = new EvaluateHiring().Execute(context.Saga.Company, context.Saga.Employees, averageProductivity);

                    if (!shouldHire) return;
                    context.Saga.PositionsNeeded = positionsNeeded;
                    context.Saga.HiringState = HiringState.Evaluating;
                    Log.Information($"Company {context.Saga.Company.Name}: Starting hiring for {positionsNeeded} positions");
                    context.TransitionToState(Hiring);
                }),

            When(EmployeeHired)
                .Then(context =>
                {
                    var employment = new Employee(
                        context.Message.HumanId,
                        context.Message.CompanyId,
                        context.Message.Salary,
                        DateTime.UtcNow,
                        EmploymentStatus.Active
                    );

                    context.Saga.Employees.Add(employment);

                    // Update company employee list
                    var updatedEmployeeIds = context.Saga.Company.EmployeeIds.ToList();
                    updatedEmployeeIds.Add(context.Message.HumanId);
                    context.Saga.Company = context.Saga.Company with { EmployeeIds = updatedEmployeeIds };
                    Log.Information($"Company {context.Saga.Company.Name}: Hired employee {context.Message.HumanId} with salary {context.Message.Salary:C}");
                }),

            When(ProductivityUpdated)
                .Then(context =>
                {
                    var employment = context.Saga.Employees.FirstOrDefault(e => e.HumanId == context.Message.HumanId);
                    if (employment == null) return;
                    var updatedEmployment = employment with { ProductivityScore = context.Message.ProductivityScore };
                    var index = context.Saga.Employees.IndexOf(employment);
                    context.Saga.Employees[index] = updatedEmployment;
                    Log.Information($"Company {context.Saga.Company.Name}: Updated productivity for employee {context.Message.HumanId} to {context.Message.ProductivityScore:F2}");
                })
        );

        During(Payroll,
            When(DayElapsed)
                .Then(context =>
                {
                    Log.Information($"Company {context.Saga.Company.Name}: Processing payroll");

                    var (totalPaid, totalTaxes, salaryPayments) = new CalculatePayroll().Execute(context.Saga.Company, context.Saga.Employees);

                    // Update company revenue (subtract payroll costs)
                    var totalPayrollCost = totalPaid + totalTaxes;
                    var updatedCompany = context.Saga.Company with { Revenue = context.Saga.Company.Revenue - totalPayrollCost };
                    context.Saga.Company = updatedCompany;

                    // Publish salary payments for each employee
                    foreach (var salaryPayment in salaryPayments)
                    {
                        context.Publish(salaryPayment);
                    }

                    // Publish payroll completed event
                    context.Publish(new PayrollCompleted(context.Saga.CorrelationId, totalPaid, totalTaxes));
                    context.Saga.PayrollState = PayrollState.Completed;
                    context.Saga.LastPayrollDate = DateTime.UtcNow;
                    Log.Information($"Company {context.Saga.Company.Name}: Payroll completed. Total paid: {totalPaid:C}, Taxes: {totalTaxes:C}");
                    context.TransitionToState(Active);
                })
        );

        During(Hiring,
            When(DayElapsed)
                .Then(context => {
                    // Create job postings for open positions
                    if (context.Saga.PositionsNeeded <= 0) return;
                    // Generate job postings for each position needed
                    for (var i = 0; i < context.Saga.PositionsNeeded; i++)
                    {
                        // Determine job level based on company size and needs
                        var jobLevel = context.Saga.Employees.Count switch
                        {
                            < 5 => JobLevel.Entry,
                            < 15 => JobLevel.Junior,
                            < 30 => JobLevel.Mid,
                            < 50 => JobLevel.Senior,
                            _ => JobLevel.Lead
                        };
                            
                        var jobPosting = new GenerateJobPosting().GenerateForCompany(
                            context.Saga.CorrelationId, 
                            context.Saga.Company.Type, 
                            jobLevel, 
                            1
                        );
                        var id = Guid.NewGuid();
                        // Publish job posting created event to start the JobPostingSaga
                        context.Publish(new CreateJobPosting(id, jobPosting.CompanyId, jobPosting));
                        Log.Information($"Company {context.Saga.Company.Name}: Created job posting for {jobPosting.Title} with salary range {jobPosting.SalaryMin:C} - {jobPosting.SalaryMax:C}");
                    }
                        
                    // Reset positions needed since we've created job postings for all
                    context.Saga.PositionsNeeded = 0;
                    context.Saga.HiringState = HiringState.HiringComplete;
                    Log.Information($"Company {context.Saga.Company.Name}: Job postings created, returning to Active state");
                    context.TransitionToState(Active);
                }),

            When(EmployeeHired)
                .Then(context =>
                {
                    // Handle external hiring events
                    var employment = new Employee(
                        context.Message.HumanId,
                        context.Message.CompanyId,
                        context.Message.Salary,
                        DateTime.UtcNow,
                        EmploymentStatus.Active
                    );

                    context.Saga.Employees.Add(employment);

                    // Update company employee list
                    var updatedEmployeeIds = context.Saga.Company.EmployeeIds.ToList();
                    updatedEmployeeIds.Add(context.Message.HumanId);
                    context.Saga.Company = context.Saga.Company with { EmployeeIds = updatedEmployeeIds };
                    context.Saga.PositionsNeeded = Math.Max(0, context.Saga.PositionsNeeded - 1);
                    Log.Information($"Company {context.Saga.Company.Name}: External hire - employee {context.Message.HumanId} with salary {context.Message.Salary:C}");

                    if (context.Saga.PositionsNeeded > 0) return;
                    context.Saga.HiringState = HiringState.HiringComplete;
                    context.TransitionToState(Active);
                })
        );

        During(WorkProgress,
            When(DayElapsed)
                .Then(context =>
                {
                    // Update individual employee productivity
                    for (var i = 0; i < context.Saga.Employees.Count; i++)
                    {
                        if (context.Saga.Employees[i].Status == EmploymentStatus.Active)
                            context.Saga.Employees[i] = new UpdateProductivity().UpdateEmployeeProductivity(context.Saga.Employees[i], random);
                    }

                    // Calculate overall productivity and revenue impact
                    var (averageProductivity, revenueChange) = new UpdateProductivity().Execute(context.Saga.Company, context.Saga.Employees);
                    context.Saga.AverageProductivity = averageProductivity;

                    // Update company revenue
                    var updatedCompany = context.Saga.Company with { Revenue = context.Saga.Company.Revenue + revenueChange };
                    context.Saga.Company = updatedCompany;
                    context.Saga.WorkProgressState = WorkProgressState.Updated;
                    Log.Information($"Company {context.Saga.Company.Name}: Work progress updated. Average productivity: {averageProductivity:F2}, Revenue change: {revenueChange:C}");
                    context.TransitionToState(Active);
                })
        );
    }
}
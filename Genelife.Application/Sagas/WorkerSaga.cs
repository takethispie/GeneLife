using Genelife.Application.Sagas.States;
using Genelife.Application.Usecases;
using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Job;
using Genelife.Messages.Commands;
using Genelife.Messages.Commands.Jobs;
using Genelife.Messages.Commands.Worker;
using Genelife.Messages.Events.Clock;
using Genelife.Messages.Events.Company;
using Genelife.Messages.Events.Jobs;
using MassTransit;
using Serilog;

namespace Genelife.Application.Sagas;

public class WorkerSaga : MassTransitStateMachine<WorkerSagaState>
{

    public State? Unemployed { get; set; }
    public State? LookingForJob { get; set; }
    public State? Working { get; set; }

    public Event<CreateWorker> Created { get; set; } = null!;
    public Event<CreateJobPosting> JobPostingCreated { get; set; } = null!;
    public Event<Recruit> ApplicationAccepted { get; set; } = null!;
    public Event<EmployeeHired> EmployeeHired { get; set; } = null!;
    public Event<DayElapsed> DayElapsed { get; set; } = null!;
    public Event<SalaryPaid>? SalaryPaid { get; set; } = null;

    public WorkerSaga()
    {
        InstanceState(x => x.CurrentState);

        Initially(
            When(Created) .Then(context => {
                    context.Saga.HumanId =  context.Message.HumanId;
                    context.Saga.SkillSet = context.Message.SkillSet;
                    context.Saga.FirstName = context.Message.Firstname;
                    context.Saga.LastName = context.Message.Lastname;
            }).TransitionTo(Unemployed)
        );
        
        Event(() => EmployeeHired, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.WorkerId.ToString()));
        Event(() => ApplicationAccepted, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CorrelationId.ToString()));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => JobPostingCreated, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => SalaryPaid, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        
        During(Unemployed, 
            When(DayElapsed).Then(bc => {
                if (bc.Saga.EmployerId != Guid.Empty || new Random().NextDouble() < 0.5) return;
                bc.TransitionToState(LookingForJob);
                Log.Information("{SagaFirstName} {SagaLastName} started actively job seeking", bc.Saga.FirstName, bc.Saga.LastName);
            }),
            Ignore(JobPostingCreated)
        );
        
        During(LookingForJob,
            When(DayElapsed).Then(bc => {
                if(bc.Saga.HiringTimeOut is 0) {
                    bc.Saga.EmployerId = Guid.Empty;
                    bc.Saga.HiringTimeOut = null;
                }
                if(bc.Saga.HiringTimeOut is > 0) bc.Saga.HiringTimeOut--;
            }),
            
            When(JobPostingCreated).Then(bc => {
                var jobPosting = bc.Message.JobPosting;
                var desiredSalary = new GenerateEmployment().GenerateDesiredSalary(bc.Saga.YearsOfExperience, jobPosting);
                var tempApplication = new JobApplication(
                    JobPostingId: bc.Message.CorrelationId,
                    HumanId: bc.Saga.CorrelationId,
                    ApplicationDate: DateTime.UtcNow,
                    RequestedSalary: desiredSalary,
                    Skills: bc.Saga.SkillSet,
                    YearsOfExperience: bc.Saga.YearsOfExperience
                );
                var matchScore = jobPosting.CalculateMatchScore(tempApplication);
                if (matchScore < 0.3f) return;
                bc.Publish(new JobApplicationSubmitted(bc.Message.CorrelationId, tempApplication with { MatchScore = matchScore }));
                Log.Information($"{bc.Saga.FirstName} {bc.Saga.LastName} applied for {jobPosting.Title} " +
                                $"(Match Score: {matchScore:F2}, Desired Salary: {desiredSalary:C})");
            }),
            
            When(EmployeeHired).Then(bc => {
                bc.Saga.HiringTimeOut = null;
                bc.Publish(new SetJobStatus(bc.Saga.HumanId, true));
                bc.Publish(new SetWorkAddress(bc.Saga.HumanId, bc.Message.CorrelationId, bc.Message.OfficeLocation));
                Log.Information("{SagaCorrelationId} finished hiring process into company {MessageCompanyId}", bc.Saga.CorrelationId, bc.Message.CorrelationId);
            }).TransitionTo(Working),
                
            When(ApplicationAccepted).Then(bc => {
                    if (bc.Saga.EmployerId != Guid.Empty) {
                        bc.Publish(new RecruitmentRefused(bc.Message.JobPostingId, bc.Saga.CorrelationId));
                        return;
                    }
                    bc.Saga.EmployerId = bc.Message.JobPosting.CompanyId;
                    bc.Publish(new RecruitmentAccepted(bc.Message.JobPostingId, bc.Saga.CorrelationId, bc.Message.JobPosting.CompanyId, bc.Message.Salary));
                    bc.Saga.HiringTimeOut = 6;
            })
        );
        
        During(Working,
            When(ApplicationAccepted).Then(bc => bc.Publish(new RecruitmentRefused(bc.Message.JobPostingId, bc.Saga.CorrelationId))),
            Ignore(JobPostingCreated),
            Ignore(DayElapsed),
            When(SalaryPaid).Then(bc =>
            {
                var newMoney = bc.Message.Amount;
                Log.Information($"{bc.Saga.CorrelationId} received salary: {bc.Message.Amount:C} " +
                                $"(tax deducted: {bc.Message.TaxDeducted:C}). " +
                                $"Total money: {newMoney:F2}");
                bc.Publish(new AddMoney(bc.Saga.HumanId, newMoney));
            })
        );
    }
}
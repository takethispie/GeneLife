using Genelife.Global.Messages.Events.Clock;
using Genelife.Work.Generators;
using Genelife.Work.Messages.Commands.Jobs;
using Genelife.Work.Messages.Commands.Worker;
using Genelife.Work.Messages.DTOs;
using Genelife.Work.Messages.Events.Company;
using Genelife.Work.Messages.Events.Jobs;
using Genelife.Work.Sagas.States;
using Genelife.Work.Usecases;
using MassTransit;
using Serilog;

namespace Genelife.Work.Sagas;

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

    public WorkerSaga()
    {
        InstanceState(x => x.CurrentState);

        Initially(
            When(Created) .Then(context => {
                    context.Saga.HumanId =  context.Message.HumanId;
                    context.Saga.SkillSet = context.Message.SkillSet;
                    context.Saga.FirstName = context.Message.Firstname;
                    context.Saga.LastName = context.Message.Lastname;
            })
                .TransitionTo(Unemployed)
        );
        
        Event(() => EmployeeHired, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.HumanId.ToString()));
        Event(() => ApplicationAccepted, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CorrelationId.ToString()));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => JobPostingCreated, e => e.CorrelateBy(saga => "any", _ => "any"));
        
        During(Unemployed, 
            When(DayElapsed).Then(bc => {
                if (bc.Saga.EmployerId != Guid.Empty || new Random().NextDouble() < 0.5) return;
                bc.TransitionToState(LookingForJob);
                Log.Information($"{bc.Saga.FirstName} {bc.Saga.LastName} started actively job seeking");
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
                var matchScore = new CalculateMatchScore().Execute(jobPosting, tempApplication);
                if (matchScore < 0.3f) return;
                bc.Publish(new JobApplicationSubmitted(bc.Message.CorrelationId, tempApplication with { MatchScore = matchScore }));
                Log.Information($"{bc.Saga.FirstName} {bc.Saga.LastName} applied for {jobPosting.Title} " +
                                $"(Match Score: {matchScore:F2}, Desired Salary: {desiredSalary:C})");
            }),
            
            When(EmployeeHired).Then(bc => {
                bc.Saga.HiringTimeOut = null;
                Log.Information($"{bc.Saga.CorrelationId} finished hiring process into company {bc.Message.CompanyId}");
                bc.TransitionToState(Working);
            }),
                
            When(ApplicationAccepted).Then(bc => {
                    if (bc.Saga.EmployerId != Guid.Empty) {
                        bc.Publish(new RecruitmentRefused(bc.Message.JobPostingId, bc.Saga.CorrelationId));
                        return;
                    }
                    bc.Saga.EmployerId = bc.Message.JobPosting.CompanyId;
                    bc.Publish(new RecruitmentAccepted(bc.Message.JobPostingId, bc.Saga.CorrelationId, bc.Message.JobPosting.CompanyId, bc.Message.salary));
                    bc.Saga.HiringTimeOut = 6;
            })
        );
        
        During(Working,
            When(DayElapsed).Then(bc => {}),
            When(ApplicationAccepted).Then(bc => bc.Publish(new RecruitmentRefused(bc.Message.JobPostingId, bc.Saga.CorrelationId))),
            Ignore(JobPostingCreated)
        );
    }
}
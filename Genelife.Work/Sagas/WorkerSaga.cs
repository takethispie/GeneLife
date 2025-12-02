using Genelife.Work.Generators;
using Genelife.Work.Messages.Commands.Jobs;
using Genelife.Work.Messages.Commands.Worker;
using Genelife.Work.Messages.DTOs;
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

    public Event<CreateWorker> Created { get; set; } = null!;
    public Event<CreateJobPosting> JobPostingCreated { get; set; }

    public WorkerSaga()
    {
        InstanceState(x => x.CurrentState);

        Initially(
            When(Created)
                .Then(context =>
                {

                })
                .TransitionTo(Unemployed)
        );
        During(LookingForJob,
            When(JobPostingCreated).Then(bc =>
            {
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
            })
        );
    }
}
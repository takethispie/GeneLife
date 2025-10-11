using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Work;
using Genelife.Main.Domain;
using Genelife.Main.Sagas.States;
using Genelife.Main.Usecases;
using Genelife.Main.Usecases.Working;
using MassTransit;
using Serilog;

namespace Genelife.Main.Sagas;

public class JobPostingSaga : MassTransitStateMachine<JobPostingSagaState>
{
    public State Active { get; set; } = null!;
    public State ReviewingApplications { get; set; } = null!;
    public State AwaitingAnswer { get; set; } = null!;

    public Event<CreateJobPosting> Created { get; set; } = null!;
    public Event<JobApplicationSubmitted> ApplicationSubmitted { get; set; } = null!;
    public Event<DayElapsed> DayElapsed { get; set; } = null!;
    public Event<RemoveApplication> RemoveApplication { get; set; } = null!;
    public Event<RecruitmentAccepted>  RecruitmentAccepted { get; set; } = null!;
    public Event<RecruitmentRefused> RecruitmentRefused { get; set; } = null!;

    public JobPostingSaga()
    {
        InstanceState(x => x.CurrentState);

        Initially(
        When(Created)
            .Then(context => {
                context.Saga.JobPosting = context.Message.JobPosting;
                context.Saga.DaysActive = 0;
                Log.Information($"Job posting created: {context.Saga.JobPosting.Title} at {context.Saga.JobPosting.CompanyId}");
            })
            .TransitionTo(Active)
        );

        // Configure event correlations
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => ApplicationSubmitted, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CorrelationId.ToString()));
        Event(() => RemoveApplication, e => e.CorrelateBy(saga => saga.JobPosting.CompanyId.ToString(), ctx => ctx.Message.CompanyId.ToString()));
        
        DuringAny(
        When(DayElapsed).Then(context => {
            if (context.Saga.DaysActive <= 10) return;
            context.Publish(new JobPostingExpired(context.Saga.CorrelationId, context.Saga.JobPosting.CompanyId));
            Log.Information($"Job posting completed: {context.Saga.JobPosting.Title}");
            context.SetCompleted();
        }));
        
        During(Active,
        When(DayElapsed)
            .Then(context =>
            {
                context.Saga.DaysActive++;
                if (context.Saga is { DaysActive: <= 3, Applications.Count: < 1 }) return;
                Log.Information($"Job posting: {context.Saga.JobPosting.Title} ended, reviewing applications");
                context.TransitionToState(ReviewingApplications);
            }),

            When(ApplicationSubmitted).Then(context => {
                var application = context.Message.Application;
                var matchScore = new CalculateMatchScore().Execute(context.Saga.JobPosting, application);
                context.Saga.Applications.Add(application with { MatchScore = matchScore });
                Log.Information($"Application received for {context.Saga.JobPosting.Title} from {context.Message.Application.HumanId} (Match: {matchScore:F2})");
            }).TransitionTo(Active),
            
            When(RemoveApplication).Then(bc => {
                var id = bc.Message.CorrelationId;
                bc.Saga.Applications = bc.Saga.Applications.Where(x => x.HumanId != id).ToList();
                Log.Information($"{id} removed from application {bc.Saga.CorrelationId}");
            }).TransitionTo(Active)
        );

        During(ReviewingApplications,
            When(DayElapsed) .Then(context => {
                var pendingApplications = context.Saga.Applications;
                if (pendingApplications.Count == 0) {
                    Log.Information($"0 Application received for {context.Saga.JobPosting.Title} closing posting");
                    context.SetCompleted();
                }

                JobApplication[] rankedApplications = [
                    ..pendingApplications
                        .OrderByDescending(app => app.MatchScore)
                        .ThenByDescending(app => app.YearsOfExperience)
                        .ThenBy(app => app.RequestedSalary)
                ];
                var topApplication = rankedApplications.FirstOrDefault();
                if (topApplication is null) return;
                if (topApplication is { MatchScore: >= 0.6f }) {
                    var salary = new CalculateOfferSalary().Execute(context.Saga.JobPosting, topApplication);
                    Log.Information($"sending offer to top candidate for {context.Saga.JobPosting.Title}: " +
                                    $"{topApplication.HumanId} (Score: {topApplication.MatchScore:F2})");
                    context.Publish(new Recruit(topApplication.HumanId, context.Saga.CorrelationId, context.Saga.JobPosting, salary));
                    context.TransitionToState(AwaitingAnswer);
                }
                else
                    context.Saga.Applications = context.Saga.Applications
                        .Where(x => x.HumanId != topApplication.HumanId).ToList();

            }),
            
            When(ApplicationSubmitted) .Then(context => {
                var application = context.Message.Application;
                var matchScore = new CalculateMatchScore().Execute(context.Saga.JobPosting, application);
                Log.Information($"Late application received for {context.Saga.JobPosting.Title} from {context.Message.Application.HumanId} (Match: {matchScore:F2})");
                context.Saga.Applications.Add(context.Message.Application);
            }).TransitionTo(Active)
        );

        During(AwaitingAnswer,
            When(RecruitmentAccepted).Then(context => {
                Log.Information($"Job filled: {context.Saga.JobPosting.Title} - Hired {context.Message.HumanId} for {context.Message.Salary:C}");
                context.Publish(new EmployeeHired(
                    context.Saga.JobPosting.CompanyId,
                    context.Message.HumanId,
                    context.Message.Salary
                ));
            }).Finalize(),
            
            When(RecruitmentRefused).Then(bc => {
                var id = bc.Message.HumanId;
                bc.Saga.Applications = bc.Saga.Applications.Where(x => x.HumanId != id).ToList();
                Log.Information($"{id} removed from application {bc.Saga.CorrelationId} after refusing recruitment proposal");
            }).TransitionTo(ReviewingApplications)
        );
        
        SetCompletedWhenFinalized();
    }
    
}
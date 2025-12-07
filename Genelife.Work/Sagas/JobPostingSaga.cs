using Genelife.Global.Messages.Events.Clock;
using Genelife.Work.Messages.Commands.Jobs;
using Genelife.Work.Messages.Events.Company;
using Genelife.Work.Messages.Events.Jobs;
using Genelife.Work.Sagas.States;
using Genelife.Work.Usecases;
using MassTransit;
using Serilog;

namespace Genelife.Work.Sagas;

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
            When(RecruitmentRefused).Then(bc => {
                var id = bc.Message.HumanId;
                bc.Saga.Applications = bc.Saga.Applications.Where(x => x.HumanId != id).ToList();
                Log.Information($"{id} removed from application {bc.Saga.CorrelationId} after refusing recruitment proposal");
                bc.TransitionToState(ReviewingApplications);
            }),
            When(DayElapsed).Then(context => context.Saga.DaysActive++)
        );
        
        During(Active,
        When(DayElapsed)
            .Then(context =>
            {
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
                    context.Publish(
                        new JobPostingExpired(context.Saga.CorrelationId, context.Saga.JobPosting.CompanyId));
                    context.SetCompleted();
                }

                var rankedApplications =
                    pendingApplications
                        .OrderByDescending(app => app.MatchScore)
                        .ThenByDescending(app => app.YearsOfExperience)
                        .ThenBy(app => app.RequestedSalary)
                        .ToList();
                if (rankedApplications is not [{ MatchScore: >= 0.6f } top, ..]) return;
                var salary = new CalculateOfferSalary().Execute(context.Saga.JobPosting, top);
                Log.Information($"sending offer to top candidate for {context.Saga.JobPosting.Title}: " +
                                $"{top.HumanId} (Score: {top.MatchScore:F2})");
                context.Publish(new Recruit(top.HumanId, context.Saga.CorrelationId, context.Saga.JobPosting, salary));
                context.TransitionToState(AwaitingAnswer);
                // whether the top applicant accepts or refuses there is no point keeping him in the applications list 
                context.Saga.Applications = context.Saga.Applications
                    .Where(x => x.HumanId != top.HumanId).ToList();
            })
        );

        During(AwaitingAnswer,
            When(RecruitmentAccepted).Then(context => {
                Log.Information($"Job filled: {context.Saga.JobPosting.Title} - Hired {context.Message.HumanId} for {context.Message.Salary:C}");
                context.Publish(new EmployeeHired(
                    context.Saga.JobPosting.CompanyId,
                    context.Message.HumanId,
                    context.Message.Salary
                ));
            }).Finalize()
        );
        
        SetCompletedWhenFinalized();
    }
    
}
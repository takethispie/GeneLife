using Genelife.Application.Sagas.States;
using Genelife.Application.Usecases;
using Genelife.Messages.Commands.Jobs;
using Genelife.Messages.Events.Clock;
using Genelife.Messages.Events.Company;
using Genelife.Messages.Events.Jobs;
using MassTransit;
using Serilog;

namespace Genelife.Application.Sagas;

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
                Log.Information("Job posting created: {JobPostingTitle} at {JobPostingCompanyId}", context.Saga.JobPosting.Title, context.Saga.JobPosting.CompanyId);
            })
            .TransitionTo(Active)
        );

        // Configure event correlations
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => ApplicationSubmitted, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => RemoveApplication, e => e.CorrelateById(saga => saga.JobPosting.CompanyId, ctx => ctx.Message.CompanyId));
        
        DuringAny(
            When(RecruitmentRefused).Then(bc => {
                var id = bc.Message.HumanId;
                bc.Saga.Applications = bc.Saga.Applications.Where(x => x.HumanId != id).ToList();
                Log.Information("{Guid} removed from application {SagaCorrelationId} after refusing recruitment proposal", id, bc.Saga.CorrelationId);
            }),
            When(DayElapsed).Then(context => context.Saga.DaysActive++)
        );
        
        During(Active,
        When(DayElapsed) .Then(context => {
                if (context.Saga is { DaysActive: <= 3, Applications.Count: < 1 }) return;
                Log.Information("Job posting: {JobPostingTitle} ended, reviewing applications", context.Saga.JobPosting.Title);
                context.TransitionToState(ReviewingApplications);
            }),

            When(ApplicationSubmitted).Then(context => {
                var application = context.Message.Application;
                var matchScore = new CalculateMatchScore().Execute(context.Saga.JobPosting, application);
                context.Saga.Applications.Add(application with { MatchScore = matchScore });
                Log.Information("Application received for {JobPostingTitle} from {ApplicationHumanId} (Match: {MatchScore:F2})", context.Saga.JobPosting.Title, context.Message.Application.HumanId, matchScore);
            }).TransitionTo(Active),
            
            When(RemoveApplication).Then(bc => {
                var id = bc.Message.CorrelationId;
                bc.Saga.Applications = bc.Saga.Applications.Where(x => x.HumanId != id).ToList();
                Log.Information("{Guid} removed from application {SagaCorrelationId}", id, bc.Saga.CorrelationId);
            }).TransitionTo(Active)
        );

        During(ReviewingApplications,
            When(DayElapsed) .Then(context => {
                var pendingApplications = context.Saga.Applications;
                if (pendingApplications.Count == 0) {
                    Log.Information("0 Application received for {JobPostingTitle} closing posting", context.Saga.JobPosting.Title);
                    context.Publish(
                        new JobPostingExpired(context.Saga.JobPosting.CompanyId, context.Saga.CorrelationId));
                    context.SetCompleted();
                }

                var rankedApplications =
                    pendingApplications
                        .OrderByDescending(app => app.MatchScore)
                        .ThenByDescending(app => app.YearsOfExperience)
                        .ThenBy(app => app.RequestedSalary)
                        .ToList();
                if (rankedApplications is not [{ MatchScore: >= 0.6f } top, ..]) return;
                var salary = context.Saga.JobPosting.CalculateSalaryOffer(top);
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
                Log.Information("Job filled: {JobPostingTitle} - Hired {MessageWorkerId} for {MessageSalary:C}", context.Saga.JobPosting.Title, context.Message.WorkerId, context.Message.Salary);
                context.Publish(new EmployeeHired(
                    context.Saga.JobPosting.CompanyId,
                    context.Message.WorkerId,
                    context.Message.Salary,
                    context.Saga.JobPosting.OfficeLocation
                ));
            }).Finalize()
        );
        
        SetCompletedWhenFinalized();
    }
    
}
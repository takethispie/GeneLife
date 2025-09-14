using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Work;
using Genelife.Main.Domain;
using Genelife.Main.Sagas.States;
using Genelife.Main.Usecases;
using MassTransit;
using Serilog;

namespace Genelife.Main.Sagas;

public class JobPostingSaga : MassTransitStateMachine<JobPostingSagaState>
{
    public State Active { get; set; } = null!;
    public State ReviewingApplications { get; set; } = null!;
    public State Filled { get; set; } = null!;
    public State Expired { get; set; } = null!;

    public Event<CreateJobPosting> Created { get; set; } = null!;
    public Event<JobApplicationSubmitted> ApplicationSubmitted { get; set; } = null!;
    public Event<DayElapsed> DayElapsed { get; set; } = null!;
    public Event<RemoveApplication> RemoveApplication { get; set; } = null!;

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
        
        During(Active,
            When(DayElapsed)
                .Then(context =>
                {
                    context.Saga.DaysActive++;
                    if (context.Saga.DaysActive > 5)
                    {
                        Log.Information($"Job posting: {context.Saga.JobPosting.Title} ended, reviewing applications");
                        context.TransitionToState(ReviewingApplications);
                        return;
                    }
                    
                    context.TransitionToState(Active);
                }),

            When(ApplicationSubmitted)
                .Then(context =>
                {
                    var application = context.Message.Application;
                    var matchScore = new CalculateMatchScore().Execute(context.Saga.JobPosting, application);
                    var updatedApplication = application with { MatchScore = matchScore };
                    context.Saga.Applications.Add(new IdentifiedJobApplication(context.Message.CorrelationId, updatedApplication));
                    Log.Information($"Application received for {context.Saga.JobPosting.Title} from {context.Message.Application.HumanId} (Match: {matchScore:F2})");
                }),
            
            When(RemoveApplication).Then(bc => {
                var id = bc.Message.CorrelationId;
                bc.Saga.Applications = bc.Saga.Applications.Where(x => x.Id != id).ToList();
                Log.Information($"{id} removed from application {bc.Saga.CorrelationId} after being hired elsewhere");
            })
        );

        During(ReviewingApplications,
            When(DayElapsed)
                .Then(context =>
                {
                    var pendingApplications = context.Saga.Applications
                        .Where(a => a.Data.Status is ApplicationStatus.Submitted or ApplicationStatus.UnderReview)
                        .ToList();
                    
                    if (pendingApplications.Count == 0)
                    {
                        context.TransitionToState(Filled);
                        return;
                    }
                    
                    IdentifiedJobApplication[] rankedApplications = [.. pendingApplications
                        .OrderByDescending(app => app.Data.MatchScore)
                        .ThenByDescending(app => app.Data.YearsOfExperience)
                        .ThenBy(app => app.Data.RequestedSalary)];
                    var topApplication = rankedApplications.FirstOrDefault();
                    if (topApplication is { Data.MatchScore: >= 0.6f })
                    {
                        // Auto-accept top candidate if match score is high enough
                        var salary = new CalculateOfferSalary().Execute(context.Saga.JobPosting, topApplication.Data);
                        
                        Log.Information($"Auto-accepting top candidate for {context.Saga.JobPosting.Title}: " +
                                        $"{topApplication.Data.HumanId} (Score: {topApplication.Data.MatchScore:F2})");
                        
                        context.Publish(new EmployeeHired(
                            context.Saga.JobPosting.CompanyId,
                            topApplication.Data.HumanId,
                            salary
                        ));
                        
                        Log.Information($"Job filled: {context.Saga.JobPosting.Title} - Hired {topApplication.Data.HumanId} for {salary:C}");
                        context.Saga.Applications = context.Saga.Applications.Where(a => a.Data.HumanId != topApplication.Data.HumanId).ToList();
                        context.TransitionToState(Filled);
                    }
                    context.TransitionToState(Active);
                }),

            When(ApplicationSubmitted)
                .Then(context =>
                {
                    var application = context.Message.Application;
                    var matchScore = new CalculateMatchScore().Execute(context.Saga.JobPosting, application);
                    Log.Information($"Late application received for {context.Saga.JobPosting.Title} from {context.Message.Application.HumanId} (Match: {matchScore:F2})");
                })
        );

        During(Filled,
            When(DayElapsed)
                .Then(context => {
                    Log.Information($"Job posting completed: {context.Saga.JobPosting.Title}");
                }).Finalize()
        );

        During(Expired,
            When(DayElapsed)
                .Then(context => {
                    Log.Information($"Job posting {context.Saga.CorrelationId} Deleted because it has expired");
                }).Finalize()
        );
        
        SetCompletedWhenFinalized();
    }
    
}
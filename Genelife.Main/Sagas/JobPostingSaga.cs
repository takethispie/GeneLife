using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Work;
using Genelife.Main.Domain;
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
    public Event<ReviewApplication> ReviewApplication { get; set; } = null!;
    public Event<DayElapsed> DayElapsed { get; set; } = null!;
    public Event<RemoveApplication> RemoveApplication { get; set; } = null!;

    public JobPostingSaga()
    {
        InstanceState(x => x.CurrentState);

        Initially(
            When(Created)
                .Then(context => {
                    context.Saga.JobPosting = context.Message.JobPosting;
                    context.Saga.CreatedDate = DateTime.UtcNow;
                    context.Saga.DaysActive = 0;
                    context.Saga.ApplicationsReceived = 0;
                    Log.Information($"Job posting created: {context.Saga.JobPosting.Title} at {context.Saga.JobPosting.CompanyId}");
                })
                .TransitionTo(Active)
        );

        // Configure event correlations
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => ApplicationSubmitted, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CorrelationId.ToString()));
        Event(() => ReviewApplication, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CorrelationId.ToString()));
        Event(() => RemoveApplication, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CorrelationId.ToString()));
        
        During(Active,
            When(DayElapsed)
                .Then(context =>
                {
                    context.Saga.DaysActive++;
                    // Check if job posting has expired
                    if (context.Saga.JobPosting.ExpiryDate.HasValue && 
                        DateTime.UtcNow >= context.Saga.JobPosting.ExpiryDate.Value)
                    {
                        Log.Information($"Job posting expired: {context.Saga.JobPosting.Title}");
                        context.Saga.JobPosting = context.Saga.JobPosting with { Status = JobPostingStatus.Expired };
                        context.TransitionToState(Expired);
                        return;
                    }
                    
                    if (context.Saga.Applications.Count < 3 || context.Saga.Applications.All(a => a.Data.Status != ApplicationStatus.Submitted)) return;
                    Log.Information($"Auto-reviewing applications for: {context.Saga.JobPosting.Title}");
                    context.TransitionToState(ReviewingApplications);
                }),

            When(ApplicationSubmitted)
                .Then(context =>
                {
                    // Calculate match score for the application
                    var application = context.Message.Application;
                    var matchScore = new CalculateMatchScore().Execute(context.Saga.JobPosting, application);
                    var updatedApplication = application with { MatchScore = matchScore };

                    context.Saga.Applications.Add(new IdentifiedJobApplication(context.Message.CorrelationId, updatedApplication));
                    context.Saga.ApplicationsReceived++;
                    Log.Information($"Application received for {context.Saga.JobPosting.Title} from {context.Message.HumanId} (Match: {matchScore:F2})");

                    context.Publish(new ApplicationStatusChanged(
                        context.Message.CorrelationId,
                        application.JobPostingId,
                        application.HumanId,
                        ApplicationStatus.UnderReview
                    ));
                    Log.Information($"Max applications reached for: {context.Saga.JobPosting.Title}");
                    if(context.Saga.Applications.Count > 2)
                        context.TransitionToState(ReviewingApplications);
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
                        context.TransitionToState(Active);
                        return;
                    }
                    
                    IdentifiedJobApplication[] rankedApplications = [.. pendingApplications
                        .OrderByDescending(app => app.Data.MatchScore)
                        .ThenByDescending(app => app.Data.YearsOfExperience)
                        .ThenBy(app => app.Data.RequestedSalary)];
                    var topApplication = rankedApplications.FirstOrDefault();
                    if (topApplication is null) return;
                    if (topApplication.Data is { MatchScore: >= 0.6m })
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
                        
                        context.Saga.SelectedApplicationId = topApplication.Id;
                        context.Saga.JobPosting = context.Saga.JobPosting with { Status = JobPostingStatus.Filled };
                        Log.Information($"Job filled: {context.Saga.JobPosting.Title} - Hired {topApplication.Data.HumanId} for {salary:C}");
                        context.Saga.Applications = context.Saga.Applications.Where(a => a.Data.HumanId != topApplication.Data.HumanId).ToList();
                        context.TransitionToState(Filled);
                    }
                    
                    foreach (var application in rankedApplications.Skip(1))
                    {
                        if (application.Data.Status == ApplicationStatus.Rejected) continue;
                        context.Publish(new ApplicationStatusChanged(
                            application.Id,
                            application.Data.JobPostingId,
                            application.Data.HumanId,
                            ApplicationStatus.Rejected
                        ));
                        Log.Information($"application rejected: {context.Saga.JobPosting.Title} for {topApplication.Data.HumanId}");
                        context.Saga.Applications = context.Saga.Applications.Where(a => a.Data.HumanId != application.Data.HumanId).ToList();
                    }
                    context.TransitionToState(Active);
                }),

            When(ApplicationSubmitted)
                .Then(context =>
                {
                    // Handle new applications during review phase
                    var application = context.Message.Application;
                    var matchScore = new CalculateMatchScore().Execute(context.Saga.JobPosting, application);
                    var updatedApplication = application with { MatchScore = matchScore };
                    context.Saga.Applications.Add(new IdentifiedJobApplication(context.Message.CorrelationId, updatedApplication));
                    context.Saga.ApplicationsReceived++;
                    Log.Information($"Late application received for {context.Saga.JobPosting.Title} from {context.Message.HumanId} (Match: {matchScore:F2})");
                    
                    context.Publish(new ApplicationStatusChanged(
                        context.Message.CorrelationId,
                        application.JobPostingId,
                        application.HumanId,
                        ApplicationStatus.UnderReview
                    ));
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
                .Then(context =>
                {
                    // Reject any remaining applications
                    var pendingApplications = context.Saga.Applications
                        .Where(a => a.Data.Status is ApplicationStatus.Submitted 
                            or ApplicationStatus.UnderReview 
                            or ApplicationStatus.Interviewing)
                        .ToList();
                    
                    foreach (var application in pendingApplications)
                    {
                        var rejectedApp = application with {
                            Data = application.Data with { Status = ApplicationStatus.Rejected }
                        };
                        var index = context.Saga.Applications.IndexOf(application);
                        context.Saga.Applications[index] = rejectedApp;
                        context.Publish(new ApplicationStatusChanged(
                            application.Id,
                            application.Data.JobPostingId,
                            application.Data.HumanId,
                            ApplicationStatus.Rejected
                        ));
                    }
                }).Finalize()
        );
        SetCompletedWhenFinalized();
    }
    
}
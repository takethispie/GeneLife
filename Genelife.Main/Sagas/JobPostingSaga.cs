using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Commands.Jobs;
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

    public Event<JobPostingCreated> Created { get; set; } = null!;
    public Event<JobApplicationSubmitted> ApplicationSubmitted { get; set; } = null!;
    public Event<ReviewApplication> ReviewApplication { get; set; } = null!;
    public Event<DayElapsed> DayElapsed { get; set; } = null!;

    private readonly MatchApplicationToJob matchApplicationToJob;
    private readonly CalculateOfferSalary calculateOfferSalary;
    private readonly Random random = new();

    public JobPostingSaga(MatchApplicationToJob matchApplicationToJobUC, CalculateOfferSalary calculateOfferSalaryUC)
    {
        matchApplicationToJob = matchApplicationToJobUC;
        calculateOfferSalary = calculateOfferSalaryUC;
        InstanceState(x => x.CurrentState);

        Initially(
            When(Created)
                .Then(context =>
                {
                    context.Saga.JobPosting = context.Message.JobPosting;
                    context.Saga.CreatedDate = DateTime.UtcNow;
                    context.Saga.DaysActive = 0;
                    context.Saga.ApplicationsReceived = 0;
                    
                    Log.Information($"Job posting created: {context.Saga.JobPosting.Title} at {context.Saga.JobPosting.CompanyId}");
                })
                .TransitionTo(Active)
        );

        // Configure event correlations
        Event(() => Created, e => e.CorrelateById(ctx => ctx.Message.JobPostingId));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => ApplicationSubmitted, e => e.CorrelateBy(saga => saga.JobPosting.Id.ToString(), ctx => ctx.Message.JobPostingId.ToString()));
        Event(() => ReviewApplication, e => e.CorrelateBy(saga => saga.JobPosting.Id.ToString(), ctx => ctx.Message.JobPostingId.ToString()));

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
                        context.Publish(new JobPostingStatusChanged(
                            context.Saga.JobPosting.Id,
                            context.Saga.JobPosting.CompanyId,
                            JobPostingStatus.Active,
                            JobPostingStatus.Expired,
                            "Job posting expired"
                        ));
                        context.TransitionToState(Expired);
                        return;
                    }
                    
                    // Auto-review applications if we have enough
                    if (context.Saga.Applications.Count >= 5 && 
                        context.Saga.Applications.Any(a => a.Status == ApplicationStatus.Submitted))
                    {
                        Log.Information($"Auto-reviewing applications for: {context.Saga.JobPosting.Title}");
                        context.TransitionToState(ReviewingApplications);
                    }
                }),

            When(ApplicationSubmitted)
                .Then(context =>
                {
                    // Calculate match score for the application
                    var application = context.Message.Application;
                    var matchScore = matchApplicationToJob.CalculateMatchScore(context.Saga.JobPosting, application);
                    var updatedApplication = application with { MatchScore = matchScore };

                    context.Saga.Applications.Add(updatedApplication);
                    context.Saga.ApplicationsReceived++;

                    Log.Information($"Application received for {context.Saga.JobPosting.Title} from {context.Message.HumanId} (Match: {matchScore:F2})");

                    // Publish application status change
                    context.Publish(new ApplicationStatusChanged(
                        application.Id,
                        application.JobPostingId,
                        application.HumanId,
                        ApplicationStatus.Submitted,
                        ApplicationStatus.UnderReview
                    ));

                    // Check if we've reached max applications
                    if (context.Saga.ApplicationsReceived < context.Saga.JobPosting.MaxApplications) return;
                    Log.Information($"Max applications reached for: {context.Saga.JobPosting.Title}");
                    context.TransitionToState(ReviewingApplications);
                }),

            When(ReviewApplication)
                .Then(context =>
                {
                    var application = context.Saga.Applications.FirstOrDefault(a => a.Id == context.Message.ApplicationId);
                    if (application == null) return;
                    var oldStatus = application.Status;
                    var updatedApplication = application with { Status = context.Message.NewStatus };
                    var index = context.Saga.Applications.IndexOf(application);
                    context.Saga.Applications[index] = updatedApplication;
                    Log.Information($"Application {context.Message.ApplicationId} status changed from {oldStatus} to {context.Message.NewStatus}");

                    context.Publish(new ApplicationStatusChanged(
                        application.Id,
                        application.JobPostingId,
                        application.HumanId,
                        oldStatus,
                        context.Message.NewStatus,
                        context.Message.ReviewNotes
                    ));

                    // If accepted, hire the employee
                    if (context.Message.NewStatus == ApplicationStatus.Accepted)
                    {
                        var salary = context.Message.OfferedSalary ?? application.RequestedSalary;
                        
                        context.Publish(new EmployeeHired(
                            context.Saga.JobPosting.CompanyId,
                            application.HumanId,
                            salary
                        ));
                        
                        context.Saga.SelectedApplicationId = application.Id;
                        context.Saga.JobPosting = context.Saga.JobPosting with { Status = JobPostingStatus.Filled };
                        Log.Information($"Job filled: {context.Saga.JobPosting.Title} - Hired {application.HumanId} for {salary:C}");
                        
                        // Reject remaining applications
                        foreach (var remainingApp in context.Saga.Applications.Where(a => a.Id != application.Id && 
                                                                                         a.Status == ApplicationStatus.UnderReview))
                        {
                            var rejectedApp = remainingApp with { Status = ApplicationStatus.Rejected };
                            var remainingIndex = context.Saga.Applications.IndexOf(remainingApp);
                            context.Saga.Applications[remainingIndex] = rejectedApp;
                            
                            context.Publish(new ApplicationStatusChanged(
                                remainingApp.Id,
                                remainingApp.JobPostingId,
                                remainingApp.HumanId,
                                ApplicationStatus.UnderReview,
                                ApplicationStatus.Rejected,
                                "Position filled by another candidate"
                            ));
                        }
                        context.TransitionToState(Filled);
                    }
                })
        );

        During(ReviewingApplications,
            When(DayElapsed)
                .Then(context =>
                {
                    // Auto-review applications based on match scores
                    var pendingApplications = context.Saga.Applications
                        .Where(a => a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview)
                        .ToList();
                    
                    if (!pendingApplications.Any())
                    {
                        context.TransitionToState(Active);
                        return;
                    }
                    
                    var rankedApplications = matchApplicationToJob.RankApplications(pendingApplications);
                    var topApplication = rankedApplications.FirstOrDefault();
                    
                    if (topApplication != null && topApplication.MatchScore >= 0.7m)
                    {
                        // Auto-accept top candidate if match score is high enough
                        var salary = calculateOfferSalary.Execute(context.Saga.JobPosting, topApplication);
                        
                        Log.Information($"Auto-accepting top candidate for {context.Saga.JobPosting.Title}: {topApplication.HumanId} (Score: {topApplication.MatchScore:F2})");
                        
                        context.Publish(new ReviewApplication(
                            topApplication.Id,
                            topApplication.JobPostingId,
                            ApplicationStatus.Accepted,
                            "Auto-accepted based on high match score",
                            salary
                        ));
                    }
                    else
                    {
                        // Review applications with lower scores
                        foreach (var application in rankedApplications.Take(3))
                        {
                            var newStatus = application.MatchScore switch
                            {
                                >= 0.6m => ApplicationStatus.Interviewing,
                                >= 0.4m => ApplicationStatus.UnderReview,
                                _ => ApplicationStatus.Rejected
                            };
                            
                            if (newStatus != application.Status)
                            {
                                var updatedApp = application with { Status = newStatus };
                                var index = context.Saga.Applications.IndexOf(application);
                                context.Saga.Applications[index] = updatedApp;
                                
                                context.Publish(new ApplicationStatusChanged(
                                    application.Id,
                                    application.JobPostingId,
                                    application.HumanId,
                                    application.Status,
                                    newStatus,
                                    $"Auto-reviewed: Match score {application.MatchScore:F2}"
                                ));
                            }
                        }
                        context.TransitionToState(Active);
                    }
                }),

            When(ApplicationSubmitted)
                .Then(context =>
                {
                    // Handle new applications during review phase
                    var application = context.Message.Application;
                    var matchScore = matchApplicationToJob.CalculateMatchScore(context.Saga.JobPosting, application);
                    var updatedApplication = application with { MatchScore = matchScore };
                    context.Saga.Applications.Add(updatedApplication);
                    context.Saga.ApplicationsReceived++;
                    Log.Information($"Late application received for {context.Saga.JobPosting.Title} from {context.Message.HumanId} (Match: {matchScore:F2})");
                    
                    context.Publish(new ApplicationStatusChanged(
                        application.Id,
                        application.JobPostingId,
                        application.HumanId,
                        ApplicationStatus.Submitted,
                        ApplicationStatus.UnderReview
                    ));
                }),

            When(ReviewApplication)
                .Then(context =>
                {
                    // Handle manual reviews during review phase
                    var application = context.Saga.Applications.FirstOrDefault(a => a.Id == context.Message.ApplicationId);
                    if (application == null) return;

                    var oldStatus = application.Status;
                    var updatedApplication = application with { Status = context.Message.NewStatus };
                    var index = context.Saga.Applications.IndexOf(application);
                    context.Saga.Applications[index] = updatedApplication;
                    context.Publish(new ApplicationStatusChanged(
                        application.Id,
                        application.JobPostingId,
                        application.HumanId,
                        oldStatus,
                        context.Message.NewStatus,
                        context.Message.ReviewNotes
                    ));

                    if (context.Message.NewStatus == ApplicationStatus.Accepted)
                    {
                        var salary = context.Message.OfferedSalary ?? application.RequestedSalary;
                        context.Publish(new EmployeeHired(
                            context.Saga.JobPosting.CompanyId,
                            application.HumanId,
                            salary
                        ));
                        
                        context.Saga.SelectedApplicationId = application.Id;
                        context.Saga.JobPosting = context.Saga.JobPosting with { Status = JobPostingStatus.Filled };
                        context.TransitionToState(Filled);
                    }
                })
        );

        During(Filled,
            When(DayElapsed)
                .Then(context =>
                {
                    // Job is filled, saga can be completed
                    Log.Information($"Job posting completed: {context.Saga.JobPosting.Title}");
                })
        );

        During(Expired,
            When(DayElapsed)
                .Then(context =>
                {
                    // Reject any remaining applications
                    var pendingApplications = context.Saga.Applications
                        .Where(a => a.Status == ApplicationStatus.Submitted || 
                                   a.Status == ApplicationStatus.UnderReview ||
                                   a.Status == ApplicationStatus.Interviewing)
                        .ToList();
                    
                    foreach (var application in pendingApplications)
                    {
                        var rejectedApp = application with { Status = ApplicationStatus.Rejected };
                        var index = context.Saga.Applications.IndexOf(application);
                        context.Saga.Applications[index] = rejectedApp;
                        context.Publish(new ApplicationStatusChanged(
                            application.Id,
                            application.JobPostingId,
                            application.HumanId,
                            application.Status,
                            ApplicationStatus.Rejected,
                            "Job posting expired"
                        ));
                    }
                })
        );
    }
    
}
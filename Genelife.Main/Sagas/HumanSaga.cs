using Genelife.Domain;
using Genelife.Domain.Commands.Cheat;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Living;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Commands.Jobs;
using Genelife.Main.Domain;
using Genelife.Main.Domain.Activities;
using Genelife.Main.Usecases;
using MassTransit;
using Serilog;
using Genelife.Domain.Generators;
using Genelife.Domain.Work;

namespace Genelife.Main.Sagas;

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State Idle { get; set; } = null!;
    public State Working { get; set; } = null;
    public State Sleeping { get; set; } = null!;
    public State Eating { get; set; } = null!;
    public State Showering { get; set; } = null!;

    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Tick> UpdateTick { get; set; } = null;
    public Event<DayElapsed> DayElapsed { get; set; } = null;
    public Event<HourElapsed> HourElapsed { get; set; } = null;
    public Event<SalaryPaid> SalaryPaid { get; set; } = null;
    public Event<CreateJobPosting> JobPostingCreated { get; set; } = null;
    public Event<EmployeeHired> EmployeeHired { get; set; } = null;
    public Event<ApplicationStatusChanged> ApplicationStatusChanged { get; set; } = null;
    public Event<SetHunger> SetHunger { get; set; } = null;
    public Event<SetAge> SetAge { get; set; } = null;
    public Event<SetEnergy> SetEnergy { get; set; } = null;
    public Event<SetHygiene> SetHygiene { get; set; } = null;
    public Event<SetMoney>  SetMoney { get; set; } = null;
    public Event<SetActivelySeekingJob> SetActivelySeekingJob { get; set; } = null!;

    public HumanSaga()
    {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            // Store the human and generate employment profile
            bc.Saga.Human = bc.Message.Human;
            bc.Saga.Employment = new GenerateEmployment().Execute(bc.Message.Human);
            Log.Information($"Created human {bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} with {bc.Saga.Employment.YearsOfExperience} years experience and {bc.Saga.Employment.Skills.Count} skills");
        }).TransitionTo(Idle));
        
        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => HourElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => SalaryPaid, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.HumanId.ToString()));
        Event(() => JobPostingCreated, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => EmployeeHired, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.HumanId.ToString()));
        Event(() => ApplicationStatusChanged, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.HumanId.ToString()));
        
        DuringAny(
            When(HourElapsed).Then(bc => {
                bc.Saga.Human = new UpdateNeeds().Execute(bc.Saga.Human);
            }),
            When(SalaryPaid).Then(bc => {
                var currentMoney = bc.Saga.Human.Money;
                var newMoney = currentMoney + (float)bc.Message.Amount;
                bc.Saga.Human = bc.Saga.Human with { Money = newMoney };
                
                Log.Information($"{bc.Saga.CorrelationId} received salary: {bc.Message.Amount:C} " +
                    $"(tax deducted: {bc.Message.TaxDeducted:C}). " +
                    $"Total money: {newMoney:F2}");
            }),
            
            When(JobPostingCreated).Then(bc => {
                // Only apply if human is actively job seeking and has employment profile
                if (bc.Saga.Employment?.IsActivelyJobSeeking != true) return;
                var jobPosting = bc.Message.JobPosting;
                var desiredSalary = new GenerateEmployment().GenerateDesiredSalary(bc.Saga.Employment, jobPosting);
                var tempApplication = new JobApplication(
                    JobPostingId: bc.Message.CorrelationId,
                    HumanId: bc.Saga.CorrelationId,
                    ApplicationDate: DateTime.UtcNow,
                    Status: ApplicationStatus.Submitted,
                    RequestedSalary: desiredSalary,
                    CoverLetter: "",
                    Skills: bc.Saga.Employment.Skills,
                    YearsOfExperience: bc.Saga.Employment.YearsOfExperience,
                    MatchScore: 0m
                );
                
                var matchScore = new CalculateMatchScore().Execute(jobPosting, tempApplication);
                if (matchScore < 0.3m) return;
                
                bc.Publish(new JobApplicationSubmitted(
                    bc.Message.CorrelationId,
                    bc.Saga.CorrelationId,
                    tempApplication with { MatchScore = matchScore }
                ));
                
                bc.Saga.Employment.SentApplicationCompanies.Add(jobPosting.CompanyId);
                
                // Update last job search date
                bc.Saga.Employment = bc.Saga.Employment with { LastJobSearchDate = DateTime.UtcNow };
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} applied for {jobPosting.Title} " +
                    $"(Match Score: {matchScore:F2}, Desired Salary: {desiredSalary:C})");
            }),
            
            When(EmployeeHired).Then(bc => {
                bc.Saga.Employment.SentApplicationCompanies.ForEach(application => {
                    bc.Publish(new RemoveApplication(bc.Saga.CorrelationId, application));
                });
                bc.Saga.Employment = bc.Saga.Employment with
                {
                    Status = EmploymentStatus.Active,
                    CurrentEmployerId = bc.Message.CompanyId,
                    CurrentSalary = bc.Message.Salary,
                    IsActivelyJobSeeking = false,
                    SentApplicationCompanies = []
                };
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} was hired by company {bc.Message.CompanyId} " +
                    $"with salary {bc.Message.Salary:C}");
            }),
            
            When(ApplicationStatusChanged).Then(bc => {
                var status = bc.Message.Status;
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} application status changed to {status}");
                bc.Saga.Employment = bc.Saga.Employment with { IsActivelyJobSeeking = true };
            }),
            When(SetAge).Then(bc => bc.Saga.Human = new ChangeBirthday().Execute(bc.Saga.Human, bc.Message.Value)),
            When(SetEnergy).Then(bc => bc.Saga.Human = bc.Saga.Human with { Energy = bc.Message.Value }),
            When(SetHunger).Then(bc => bc.Saga.Human = bc.Saga.Human with { Hunger = bc.Message.Value }),
            When(SetHygiene).Then(bc => bc.Saga.Human = bc.Saga.Human with { Hygiene = bc.Message.Value }),
            When(SetActivelySeekingJob).Then(bc => {
                bc.Saga.Employment ??= new GenerateEmployment().Execute(bc.Saga.Human);
                bc.Saga.Employment = bc.Saga.Employment with { IsActivelyJobSeeking = bc.Message.Value };
            }),
            When(DayElapsed).Then(bc => {
                Log.Information($"{bc.Saga.CorrelationId} " +
                                $"needs: {bc.Saga.Human.Hunger} hunger " +
                                $"and {bc.Saga.Human.Energy} energy " +
                                $"and {bc.Saga.Human.Hygiene} hygiene "
                );
                if (bc.Saga.Employment?.Status != EmploymentStatus.Unemployed) return;
                //don't always go straight to jobseeking when unemployed
                if (bc.Saga.Employment.IsActivelyJobSeeking || !(new Random().NextDouble() < 0.5)) return; 
                bc.Saga.Employment = bc.Saga.Employment with { IsActivelyJobSeeking = true };
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} started actively job seeking");
            })
        );
        
        During(Idle, 
            When(UpdateTick).Then(bc => {
                var activity = new ChooseActivity().Execute(bc.Saga.Human, bc.Message.Hour);
                var state = activity switch {
                    Eat => Eating,
                    Sleep => Sleeping,
                    Shower => Showering,
                    Work => Working,
                    _ => Idle
                };
                if (activity is null) bc.Saga.Activity = null;
                else {
                    if(activity.ToEnum() != bc.Saga.Activity) Log.Information($"{bc.Saga.CorrelationId} is transitioning to {state}");
                    bc.Saga.Activity = activity.ToEnum();
                    bc.Saga.ActivityTickDuration = activity.TickDuration;
                    bc.Saga.Human = activity.Apply(bc.Saga.Human);
                }
                bc.TransitionToState(state);
            })
        );
        
        During(Eating, Sleeping, Showering,
            When(UpdateTick).Then(bc => {
                if (bc.Saga.ActivityTickDuration >= 0) {
                    bc.Saga.ActivityTickDuration -= 1;
                    return;
                }
                Log.Information($"{bc.Saga.CorrelationId} has finished {bc.Saga.CurrentState}");
                bc.Saga.Activity = null;
                bc.TransitionToState(Idle);
            }),
            When(DayElapsed).Then(_ => {})
        );
    }
}
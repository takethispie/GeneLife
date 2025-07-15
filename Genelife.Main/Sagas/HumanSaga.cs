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
    public Event<EmployeeHired> HireEmployee { get; set; } = null;
    public Event<ApplicationStatusChanged> ApplicationStatusChanged { get; set; } = null;
    public Event<SetHunger> SetHunger { get; set; } = null;
    public Event<SetAge> SetAge { get; set; } = null;
    public Event<SetEnergy> SetEnergy { get; set; } = null;
    public Event<SetHygiene> SetHygiene { get; set; } = null;
    public Event<SetMoney>  SetMoney { get; set; } = null;

    private readonly Random random = new();


    public HumanSaga()
    {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            // Store the human and generate employment profile
            bc.Saga.Human = bc.Message.Human;
            bc.Saga.EmploymentProfile = new GenerateEmployment().Execute(bc.Message.Human);
            Log.Information($"Created human {bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} with {bc.Saga.EmploymentProfile.YearsOfExperience} years experience and {bc.Saga.EmploymentProfile.Skills.Count} skills");
        }).TransitionTo(Idle));
        
        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => HourElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => SalaryPaid, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.HumanId.ToString()));
        Event(() => JobPostingCreated, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => HireEmployee, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.HumanId.ToString()));
        Event(() => ApplicationStatusChanged, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.HumanId.ToString()));
        
        DuringAny(
            When(HourElapsed).Then(bc => {
                bc.Saga.Human = new UpdateNeeds().Execute(bc.Saga.Human);
                Log.Information($"{bc.Saga.CorrelationId} " +
                    $"needs: {bc.Saga.Human.Hunger} hunger " +
                    $"and {bc.Saga.Human.Energy} energy " +
                    $"and {bc.Saga.Human.Hygiene} hygiene "
                );
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
                if (bc.Saga.EmploymentProfile?.IsActivelyJobSeeking != true) return;
                
                var jobPosting = bc.Message.JobPosting;
                
                // Calculate interest in this job based on match score
                var tempApplication = new Genelife.Domain.JobApplication(
                    JobPostingId: bc.Message.CorrelationId,
                    HumanId: bc.Saga.CorrelationId,
                    ApplicationDate: DateTime.UtcNow,
                    Status: Genelife.Domain.ApplicationStatus.Submitted,
                    RequestedSalary: new GenerateEmployment().GenerateDesiredSalary(bc.Saga.EmploymentProfile, jobPosting),
                    CoverLetter: "",
                    Skills: bc.Saga.EmploymentProfile.Skills,
                    YearsOfExperience: bc.Saga.EmploymentProfile.YearsOfExperience,
                    MatchScore: 0m
                );
                
                var matchScore = new CalculateMatchScore().Execute(jobPosting, tempApplication);
                
                // Apply based on match score and some randomness
                var shouldApply = matchScore >= 0.3m && random.NextDouble() < (double)matchScore;
                
                if (!shouldApply) return;
                
                var desiredSalary = new GenerateEmployment().GenerateDesiredSalary(bc.Saga.EmploymentProfile, jobPosting);
                var coverLetter = new GenerateEmployment().GenerateCoverLetter(bc.Saga.Human, bc.Saga.EmploymentProfile, jobPosting);
                
                bc.Publish(new SubmitJobApplication(
                    bc.Message.CorrelationId,
                    bc.Saga.CorrelationId,
                    desiredSalary,
                    coverLetter,
                    bc.Saga.EmploymentProfile.Skills,
                    bc.Saga.EmploymentProfile.YearsOfExperience
                ));
                
                // Update last job search date
                bc.Saga.EmploymentProfile = bc.Saga.EmploymentProfile with { LastJobSearchDate = DateTime.UtcNow };
                
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} applied for {jobPosting.Title} " +
                    $"(Match Score: {matchScore:F2}, Desired Salary: {desiredSalary:C})");
            }),
            
            When(HireEmployee).Then(bc => {
                bc.Saga.EmploymentProfile = bc.Saga.EmploymentProfile with
                {
                    EmploymentStatus = EmploymentStatus.Active,
                    CurrentEmployerId = bc.Message.CompanyId,
                    CurrentSalary = bc.Message.Salary,
                    IsActivelyJobSeeking = false
                };
                
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} was hired by company {bc.Message.CompanyId} " +
                    $"with salary {bc.Message.Salary:C}");
            }),
            
            When(ApplicationStatusChanged).Then(bc => {
                var status = bc.Message.NewStatus;
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} application status changed to {status}");
                
                // If rejected, might become more active in job searching
                if (status == Genelife.Domain.ApplicationStatus.Rejected && 
                    bc.Saga.EmploymentProfile?.EmploymentStatus == EmploymentStatus.Unemployed)
                {
                    bc.Saga.EmploymentProfile = bc.Saga.EmploymentProfile with { IsActivelyJobSeeking = true };
                }
            }),
            When(SetAge).Then(bc => bc.Saga.Human = new ChangeBirthday().Execute(bc.Saga.Human, bc.Message.Value)),
            When(SetEnergy).Then(bc => bc.Saga.Human = bc.Saga.Human with { Energy = bc.Message.Value }),
            When(SetHunger).Then(bc => bc.Saga.Human = bc.Saga.Human with { Hunger = bc.Message.Value }),
            When(SetHygiene).Then(bc => bc.Saga.Human = bc.Saga.Human with { Hygiene = bc.Message.Value })
        );
        
        During(Idle, 
            When(UpdateTick).Then(bc => {
                var activity = new ChooseActivity().Execute(bc.Saga.Human, bc.Message.Hour);
                var state = activity switch {
                    Eat eat => Eating,
                    Sleep sleep => Sleeping,
                    Shower shower => Showering,
                    Work work => Working,
                    _ => Idle
                };
                Log.Information($"{bc.Saga.CorrelationId} is transitioning to {state}");
                if (activity is null) bc.Saga.Activity = null;
                else {
                    bc.Saga.Activity = activity.ToEnum();
                    bc.Saga.ActivityTickDuration = activity.TickDuration;
                    bc.Saga.Human = activity.Apply(bc.Saga.Human);
                }
                bc.TransitionToState(state);
            }),
            When(DayElapsed).Then(bc => {
                // Periodically update job seeking behavior
                if (bc.Saga.EmploymentProfile?.EmploymentStatus != EmploymentStatus.Unemployed) return;
                // Unemployed people become more active in job seeking over time
                if (bc.Saga.EmploymentProfile.IsActivelyJobSeeking || !(random.NextDouble() < 0.5)) return; 
                bc.Saga.EmploymentProfile = bc.Saga.EmploymentProfile with { IsActivelyJobSeeking = true };
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} started actively job seeking");
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
            When(DayElapsed).Then(bc => {})
        );
    }
}
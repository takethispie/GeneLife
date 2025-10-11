using System.Diagnostics;
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
using Genelife.Main.Sagas.States;
using Genelife.Main.Usecases.Working;

namespace Genelife.Main.Sagas;

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State? Idle { get; set; } = null!;
    public State? Working { get; set; } = null;
    public State? Sleeping { get; set; } = null!;
    public State? Eating { get; set; } = null!;
    public State? Showering { get; set; } = null!;

    public Event<CreateHuman>? Created { get; set; } = null;
    public Event<Tick>? UpdateTick { get; set; } = null;
    public Event<DayElapsed>? DayElapsed { get; set; } = null;
    public Event<HourElapsed>? HourElapsed { get; set; } = null;
    public Event<SalaryPaid>? SalaryPaid { get; set; } = null;
    public Event<CreateJobPosting>? JobPostingCreated { get; set; } = null;
    public Event<EmployeeHired>? EmployeeHired { get; set; } = null;
    public Event<SetHunger>? SetHunger { get; set; } = null;
    public Event<SetAge>? SetAge { get; set; } = null;
    public Event<SetEnergy>? SetEnergy { get; set; } = null;
    public Event<SetHygiene>? SetHygiene { get; set; } = null;
    public Event<SetMoney>?  SetMoney { get; set; } = null;
    public Event<SetActivelySeekingJob> SetActivelySeekingJob { get; set; } = null!;
    public Event<Recruit> ApplicationAccepted { get; set; } = null!;

    public HumanSaga()
    {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            // Store the human and generate employment profile
            bc.Saga.Human = bc.Message.Human;
            (bc.Saga.SkillSet, bc.Saga.YearsOfExperience) = new GenerateEmployment().Execute(bc.Message.Human);
            Log.Information($"Created human {bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} with {bc.Saga.YearsOfExperience} years experience and {bc.Saga.SkillSet.Count} skills");
        }).TransitionTo(Idle));
        
        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => HourElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => SalaryPaid, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CorrelationId.ToString()));
        Event(() => JobPostingCreated, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => EmployeeHired, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.HumanId.ToString()));
        Event(() => ApplicationAccepted, e => e.CorrelateBy(saga => saga.CorrelationId.ToString(), ctx => ctx.Message.CorrelationId.ToString()));
        
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
                if (bc.Saga.SeekingJob is not true) return;
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
                bc.Publish(new JobApplicationSubmitted(bc.Message.CorrelationId, tempApplication with { MatchScore = matchScore } ));
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} applied for {jobPosting.Title} " +
                    $"(Match Score: {matchScore:F2}, Desired Salary: {desiredSalary:C})");
            }),
            
            When(EmployeeHired).Then(bc => {
                bc.Saga.HiringTimeOut = null;
                bc.Saga.SeekingJob = false;
                Log.Information($"{bc.Saga.CorrelationId} finished hiring process into company {bc.Message.CompanyId}");
            }),
            
            When(ApplicationAccepted).Then(bc => {
                if (bc.Saga.EmployerId != Guid.Empty) {
                    bc.Publish(new RecruitmentRefused(bc.Message.JobPostingId, bc.Saga.CorrelationId));
                    return;
                }
                bc.Saga.EmployerId = bc.Message.JobPosting.CompanyId;
                bc.Publish(new RecruitmentAccepted(bc.Message.JobPostingId, bc.Saga.CorrelationId, bc.Message.JobPosting.CompanyId, bc.Message.salary));
                bc.Saga.HiringTimeOut = 6;
            }),
            
            

            When(SetAge).Then(bc => bc.Saga.Human = new ChangeBirthday().Execute(bc.Saga.Human, bc.Message.Value)),
            When(SetEnergy).Then(bc => bc.Saga.Human = bc.Saga.Human with { Energy = bc.Message.Value }),
            When(SetHunger).Then(bc => bc.Saga.Human = bc.Saga.Human with { Hunger = bc.Message.Value }),
            When(SetHygiene).Then(bc => bc.Saga.Human = bc.Saga.Human with { Hygiene = bc.Message.Value }),
            When(SetActivelySeekingJob).Then(bc => bc.Saga.SeekingJob = bc.Message.Value),
            When(DayElapsed).Then(bc => {
                Log.Information($"{bc.Saga.CorrelationId} " +
                                $"needs: {bc.Saga.Human.Hunger} hunger " +
                                $"and {bc.Saga.Human.Energy} energy " +
                                $"and {bc.Saga.Human.Hygiene} hygiene " +
                                $"and {bc.Saga.Human.Money} money "
                );
                if(bc.Saga.HiringTimeOut is 0) {
                    bc.Saga.EmployerId = Guid.Empty;
                    bc.Saga.HiringTimeOut = null;
                }
                if(bc.Saga.HiringTimeOut is > 0) bc.Saga.HiringTimeOut--;
                if (bc.Saga.EmployerId != Guid.Empty || bc.Saga.SeekingJob || new Random().NextDouble() < 0.5) return;
                bc.Saga.SeekingJob = true;
                Log.Information($"{bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} started actively job seeking");
            })
        );
        
        During(Idle, 
            When(UpdateTick).Then(bc => {
                var activity = new ChooseActivity().Execute(bc.Saga.Human, bc.Message.Hour, bc.Saga.EmployerId != Guid.Empty);
                var state = activity switch {
                    Eat => Eating,
                    Sleep => Sleeping,
                    Shower => Showering,
                    Work => Working,
                    _ => Idle
                };
                if (activity is null) bc.Saga.Activity = null;
                else {
                    bc.Saga.Activity = activity;
                    bc.Saga.Human = activity.Apply(bc.Saga.Human);
                }
                bc.TransitionToState(state);
            })
        );
        
        During(Eating, Sleeping, Showering, Working,
            When(UpdateTick).Then(bc => {
                if (bc.Saga.Activity?.TickDuration > 0) {
                    bc.Saga.Activity.TickDuration -= 1;
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
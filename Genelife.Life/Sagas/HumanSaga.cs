using System.Numerics;
using Genelife.Global.Messages.Commands.Locomotion;
using Genelife.Global.Messages.DTOs;
using Genelife.Global.Messages.Events.Clock;
using Genelife.Global.Messages.Events.Locomotion;
using Genelife.Life.Domain.Activities;
using Genelife.Life.Domain.Address;
using Genelife.Life.Domain.Exceptions;
using Genelife.Life.Messages.Commands;
using Genelife.Life.Sagas.States;
using Genelife.Life.Usecases;
using Genelife.Work.Messages.Events.Company;
using MassTransit;
using Serilog;

namespace Genelife.Life.Sagas;

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
    public Event<SetJobStatus>?  JobStatusChanged { get; set; } = null;
    public Event<SetHunger>? SetHunger { get; set; } = null;
    public Event<SetAge>? SetAge { get; set; } = null;
    public Event<SetEnergy>? SetEnergy { get; set; } = null;
    public Event<SetHygiene>? SetHygiene { get; set; } = null;
    public Event<SetMoney>? SetMoney { get; set; } = null;
    public Event<Arrived>? Arrived { get; set; } = null;
    public Event<SetHomeAddress> AddHomeAddress { get; set; } = null!;
    public Event<SetWorkAddress> AddWorkAddress { get; set; } = null!;
    public Event<LeaveWork> LeaveWork { get; set; } = null!;
    public Event<LeaveHome> LeaveHome { get; set; } = null!;
    public Event<GoToWork> GoToWork { get; set; } = null!;
    public Event<GoHome> GoHome { get; set; } = null!;



    public HumanSaga()
    {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc =>
        {
            bc.Saga.Human = bc.Message.Human;
            Log.Information($"Created human {bc.Saga.Human.FirstName} {bc.Saga.Human.LastName} ");
        }).TransitionTo(Idle));

        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => HourElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => SalaryPaid, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => JobStatusChanged, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => Arrived, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => AddHomeAddress, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => AddWorkAddress, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => Arrived, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => LeaveHome, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => LeaveWork, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => GoHome, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => GoToWork, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));

        DuringAny(
            When(HourElapsed).Then(bc => { bc.Saga.Human = new UpdateNeeds().Execute(bc.Saga.Human); }),
            When(SalaryPaid).Then(bc =>
            {
                var currentMoney = bc.Saga.Human.Money;
                var newMoney = currentMoney + bc.Message.Amount;
                bc.Saga.Human = bc.Saga.Human with { Money = newMoney };
                Log.Information($"{bc.Saga.CorrelationId} received salary: {bc.Message.Amount:C} " +
                                $"(tax deducted: {bc.Message.TaxDeducted:C}). " +
                                $"Total money: {newMoney:F2}");
            }),
            When(SetAge).Then(bc => bc.Saga.Human = new ChangeBirthday().Execute(bc.Saga.Human, bc.Message.Value)),
            When(SetEnergy).Then(bc => bc.Saga.Human = bc.Saga.Human with { Energy = bc.Message.Value }),
            When(SetHunger).Then(bc => bc.Saga.Human = bc.Saga.Human with { Hunger = bc.Message.Value }),
            When(SetHygiene).Then(bc => bc.Saga.Human = bc.Saga.Human with { Hygiene = bc.Message.Value }),
            When(SetMoney).Then(bc => bc.Saga.Human = bc.Saga.Human with { Money = bc.Message.Value }),
            When(DayElapsed).Then(bc =>
            {
                Log.Information($"{bc.Saga.CorrelationId} " +
                                $"needs: {Math.Round(bc.Saga.Human.Hunger)} hunger " +
                                $" {Math.Round(bc.Saga.Human.Energy)} energy " +
                                $" {Math.Round(bc.Saga.Human.Hygiene)} hygiene " +
                                $" {bc.Saga.Human.Money} money "
                );
            }),
            When(JobStatusChanged).Then(bc =>
            {
                var message =  bc.Message.Hasjob 
                    ? "Work activity added to possible activities" 
                    : "Work activity removed from possible activities";
                Log.Information($"{bc.Saga.CorrelationId} has {message}");
                bc.Saga.HasJob = bc.Message.Hasjob;
            }),
            When(AddHomeAddress).Then(bc =>
            {
                bc.Saga.AddressBook.Add(new AddressEntry(AddressType.Home, bc.Message.HomeId));
            }),
            When(AddWorkAddress).Then(bc =>
            {
                bc.Saga.AddressBook.Add(new AddressEntry(AddressType.Office, bc.Message.OfficeId));
            }),
            When(Arrived) .Then(bc => bc.Saga.Position =
                new Position(
                    new Vector3(bc.Message.X, bc.Message.Y, bc.Message.Z), 
                    bc.Message.LocationName
                )
            ),
            When(GoToWork).Then(OnGoToWork),
            When(LeaveWork).Then(OnLeaveWork),
            When(GoHome).Then(bc => OnGoHome(bc.Saga.AddressBook, bc, bc.Saga.CorrelationId))
        );

        During(Idle,
            When(UpdateTick).Then(bc =>
            {
                var activity = new ChooseActivity().Execute(bc.Saga.Human, bc.Message.Hour, bc.Saga.HasJob);
                var state = activity switch
                {
                    Eat => Eating,
                    Sleep => Sleeping,
                    Shower => Showering,
                    Domain.Activities.Work => Working,
                    _ => Idle
                };
                bc.Saga.Activity = activity;
                bc.Saga.Human = activity.Apply(bc.Saga.Human);
                if (activity is Domain.Activities.Work)
                    bc.Publish(new GoToWork(bc.Saga.CorrelationId));
                bc.TransitionToState(state);
            })
        );

        During(Eating, Sleeping, Showering, Working,
            When(UpdateTick).Then(bc =>
            {
                if (bc.Saga.Activity.TickDuration > 0)
                {
                    bc.Saga.Activity.TickDuration -= 1;
                    return;
                }

                Log.Information($"{bc.Saga.CorrelationId} has finished {bc.Saga.CurrentState}");
                if (bc.Saga.Activity is Domain.Activities.Work { GoHomeWhenFinished: true })
                    bc.Publish(new LeaveWork(bc.Saga.CorrelationId));
                bc.Saga.Activity = new Idle();
                bc.TransitionToState(Idle);
            }),
            Ignore(DayElapsed)
        );
    }

    private static void OnGoToWork(BehaviorContext<HumanSagaState, GoToWork> bc)
    {
        var workAddress = bc.Saga.AddressBook.AllOfAddressType(AddressType.Office).FirstOrDefault();
        //TODO use fallback system-wide event with just the human correlationId and no target office
        if (workAddress is null) 
            throw new AddressNotFoundException(nameof(workAddress));
        bc.Publish(new EnteredWork(bc.Saga.CorrelationId, workAddress.EntityId));
    }

    private static void OnLeaveWork(BehaviorContext<HumanSagaState, LeaveWork> bc)
    {
        var workAddress = bc.Saga.AddressBook.AllOfAddressType(AddressType.Office).FirstOrDefault();
        //TODO use fallback system-wide event with just the human correlationId and no target office
        if (workAddress is null) 
            throw new AddressNotFoundException(nameof(workAddress));
        bc.Publish(new LeftWork(workAddress.EntityId, bc.Saga.CorrelationId));
        OnGoHome(bc.Saga.AddressBook, bc, bc.Saga.CorrelationId);
    }

    private static void OnGoHome(AddressBook addressBook, IPublishEndpoint endpoint, Guid correlationId)
    {
        var homeAddress = addressBook
            .AllOfAddressType(AddressType.Home)
            .FirstOrDefault();
        if (homeAddress is null) 
            throw new AddressNotFoundException(nameof(homeAddress));
        var pos = homeAddress.EntityId;
        endpoint.Publish(new GoHome(pos, correlationId));
    }
    
}
using System.Numerics;
using Genelife.Global.Messages.Commands.Grocery;
using Genelife.Global.Messages.Commands.Locomotion;
using Genelife.Global.Messages.DTOs;
using Genelife.Global.Messages.Events;
using Genelife.Global.Messages.Events.Clock;
using Genelife.Global.Messages.Events.Grocery;
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
    public State? Drinking { get; set; } = null!;
    public State? Showering { get; set; } = null!;
    public State? Shopping { get; set; } = null!;

    public Event<CreateHuman>? Created { get; set; } = null;
    public Event<Tick>? UpdateTick { get; set; } = null;
    public Event<DayElapsed>? DayElapsed { get; set; } = null;
    public Event<HourElapsed>? HourElapsed { get; set; } = null;
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
    public Event<AddMoney>? AddMoney { get; set; } = null;
    public Event<FoodPurchased>? FoodPurchased { get; set; } = null;
    public Event<DrinkPurchased>? DrinkPurchased { get; set; } = null;
    public Event<EnteredGroceryStore>? EnteredGroceryStore { get; set; } = null;
    public Event<LeftGroceryStore>? LeftGroceryStore { get; set; } = null;



    public HumanSaga()
    {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc =>
        {
            bc.Saga.Human = bc.Message.Human;
            bc.Saga.AddressBook = new AddressBook();
            Log.Information("Created human {HumanFirstName} {HumanLastName} ", bc.Saga.Human.FirstName, bc.Saga.Human.LastName);
        }).TransitionTo(Idle));

        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => HourElapsed, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => JobStatusChanged, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => Arrived, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => AddHomeAddress, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => AddWorkAddress, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => Arrived, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => LeaveHome, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => LeaveWork, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => GoHome, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => GoToWork, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => AddMoney, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => FoodPurchased, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.HumanId));
        Event(() => DrinkPurchased, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.HumanId));
        Event(() => EnteredGroceryStore, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.HumanId));
        Event(() => LeftGroceryStore, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.HumanId));
        DuringAny(
            When(HourElapsed).Then(bc => { bc.Saga.Human = new UpdateNeeds().Execute(bc.Saga.Human); }),
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
                var message = bc.Message.Hasjob
                    ? "Work activity added to possible activities"
                    : "Work activity removed from possible activities";
                Log.Information("{SagaCorrelationId} has {Message}", bc.Saga.CorrelationId, message);
                bc.Saga.HasJob = bc.Message.Hasjob;
            }),
            When(AddHomeAddress).Then(bc =>
            {
                Log.Information("home address added for human {SagaCorrelationId}", bc.Saga.CorrelationId);
                var coordinates = new AddressCoordinates(
                    bc.Message.Coordinates.X,
                    bc.Message.Coordinates.Y,
                    bc.Message.Coordinates.Z
                );
                bc.Saga.AddressBook.Add(new AddressEntry(AddressType.Home, bc.Message.HomeId, coordinates));
            }),
            When(AddWorkAddress).Then(bc =>
            {
                Log.Information("work address added for human {SagaCorrelationId}", bc.Saga.CorrelationId);
                var coordinates = new AddressCoordinates(
                    bc.Message.OfficeLocation.X,
                    bc.Message.OfficeLocation.Y,
                    bc.Message.OfficeLocation.Z
                );
                bc.Saga.AddressBook.Add(new AddressEntry(AddressType.Office, bc.Message.OfficeId, coordinates));
            }),
            When(Arrived).Then(bc => bc.Saga.Position = new Position(bc.Message.X, bc.Message.Y, bc.Message.Z)),
            When(GoToWork).Then(OnGoToWork),
            When(LeaveWork).Then(OnLeaveWork),
            When(GoHome).Then(bc => OnGoHome(bc.Saga.AddressBook, bc, bc.Saga.CorrelationId)),
            When(AddMoney)
                .Then(bc => bc.Saga.Human = bc.Saga.Human with {Money = bc.Saga.Human.Money + bc.Message.Amount }),
            When(FoodPurchased).Then(bc =>
            {
                bc.Saga.FoodCount++;
                Log.Information("Human {HumanId} purchased food. Total food items: {FoodCount}",
                    bc.Saga.CorrelationId, bc.Saga.FoodCount);
            }),
            When(DrinkPurchased).Then(bc =>
            {
                bc.Saga.DrinkCount++;
                Log.Information("Human {HumanId} purchased drink. Total drink items: {DrinkCount}",
                    bc.Saga.CorrelationId, bc.Saga.DrinkCount);
            })
        );

        During(Idle, 
            When(UpdateTick).Then(bc =>
            {
                var activity = new ChooseActivity().Execute(bc.Saga.Human, bc.Message.Hour, bc.Saga.HasJob);
                switch (activity)
                {
                    case Eat when bc.Saga.FoodCount <= 0:
                    case Drink when bc.Saga.DrinkCount <= 0:
                    {
                        var storeAddress = bc.Saga.AddressBook.AllOfAddressType(AddressType.Store).FirstOrDefault();
                        if (storeAddress != null)
                        {
                            bc.Publish(new GoToGroceryStore(bc.Saga.CorrelationId, storeAddress.EntityId));
                            bc.TransitionToState(Shopping);
                            return;
                        }
                        break;
                    }
                }

                var state = activity switch
                {
                    Eat => Eating,
                    Drink => Drinking,
                    Sleep => Sleeping,
                    Shower => Showering,
                    Domain.Activities.Work => Working,
                    _ => Idle
                };
                bc.Saga.Activity = activity;
                
                switch (activity)
                {
                    case Eat when bc.Saga.FoodCount > 0:
                        bc.Saga.FoodCount--;
                        bc.Saga.Human = activity.Apply(bc.Saga.Human);
                        Log.Information("Human {HumanId} consumed food. Remaining food: {FoodCount}",
                            bc.Saga.CorrelationId, bc.Saga.FoodCount);
                        break;
                    
                    case Drink when bc.Saga.DrinkCount > 0:
                        bc.Saga.DrinkCount--;
                        bc.Saga.Human = activity.Apply(bc.Saga.Human);
                        Log.Information("Human {HumanId} consumed drink. Remaining drinks: {DrinkCount}",
                            bc.Saga.CorrelationId, bc.Saga.DrinkCount);
                        break;
                    
                    default:
                        if (activity is not Eat and not Drink)
                            bc.Saga.Human = activity.Apply(bc.Saga.Human);
                        break;
                }
                
                if (activity is Domain.Activities.Work)
                    bc.Publish(new GoToWork(bc.Saga.CorrelationId));
                    
                bc.TransitionToState(state);
            })
        );

        During(Eating, Drinking, Sleeping, Showering,
            When(UpdateTick).Then(bc =>
            {
                if (bc.Saga.Activity.TickDuration > 0)
                {
                    bc.Saga.Activity.TickDuration -= 1;
                    return;
                }

                Log.Information("{SagaCorrelationId} has finished {SagaCurrentState}", bc.Saga.CorrelationId, bc.Saga.CurrentState);
                bc.Saga.Activity = new Idle();
                bc.TransitionToState(Idle);
            }),
            Ignore(DayElapsed)
        );

        During(Working,
            When(UpdateTick).Then(bc =>
            {
                if (bc.Saga.Activity.TickDuration > 0)
                {
                    bc.Saga.Activity.TickDuration -= 1;
                    return;
                }

                Log.Information("{SagaCorrelationId} has finished {SagaCurrentState}", bc.Saga.CorrelationId, bc.Saga.CurrentState);
                if (bc.Saga.Activity is Domain.Activities.Work { GoHomeWhenFinished: true })
                    bc.Publish(new LeaveWork(bc.Saga.CorrelationId));
                bc.Saga.Activity = new Idle();
                bc.TransitionToState(Idle);
            }),
            Ignore(DayElapsed)
        );

        During(Shopping,
            When(EnteredGroceryStore).Then(bc =>
            {
                if (bc.Saga.FoodCount <= 0)
                    bc.Publish(new BuyFood(bc.Saga.CorrelationId, bc.Message.GroceryStoreId));
                if (bc.Saga.DrinkCount <= 0)
                    bc.Publish(new BuyDrink(bc.Saga.CorrelationId, bc.Message.GroceryStoreId));
                bc.Publish(new LeaveGroceryStore(bc.Saga.CorrelationId, bc.Message.GroceryStoreId));
            }),
            When(LeftGroceryStore).Then(bc =>
            {
                Log.Information("Human {HumanId} finished shopping and left grocery store", bc.Saga.CorrelationId);
                bc.Saga.Activity = new Idle();
                bc.TransitionToState(Idle);
            })
        );
    }

    private static void OnGoToWork(BehaviorContext<HumanSagaState, GoToWork> bc)
    {
        var workAddress = bc.Saga.AddressBook.AllOfAddressType(AddressType.Office).FirstOrDefault();
        //TODO use fallback system-wide event with just the human correlationId and no target office
        if (workAddress is null) 
            throw new AddressNotFoundException(nameof(workAddress));
        var homeAddress = bc.Saga.AddressBook
            .AllOfAddressType(AddressType.Home)
            .FirstOrDefault();
        if (homeAddress is null) 
            throw new AddressNotFoundException(nameof(homeAddress));
        bc.Publish(new LeaveHome(homeAddress.EntityId, bc.Saga.CorrelationId));
        bc.Publish(new EnteredWork(bc.Saga.CorrelationId, workAddress.EntityId));
        Log.Information("{SagaCorrelationId} is going to work at {WorkAddressEntityId}", bc.Saga.CorrelationId, workAddress.EntityId);
    }

    private static void OnLeaveWork(BehaviorContext<HumanSagaState, LeaveWork> bc)
    {
        var workAddress = bc.Saga.AddressBook.AllOfAddressType(AddressType.Office).FirstOrDefault();
        //TODO use fallback system-wide event with just the human correlationId and no target office
        if (workAddress is null) 
            throw new AddressNotFoundException(nameof(workAddress));
        bc.Publish(new LeftWork(workAddress.EntityId, bc.Saga.CorrelationId));
        OnGoHome(bc.Saga.AddressBook, bc, bc.Saga.CorrelationId);
        Log.Information("{SagaCorrelationId} is leaving work at {WorkAddressEntityId}", bc.Saga.CorrelationId, workAddress.EntityId);
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
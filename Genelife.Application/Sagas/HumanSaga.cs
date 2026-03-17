using Genelife.Application.IntegrationEvents;
using Genelife.Application.Sagas.States;
using Genelife.Domain;
using Genelife.Domain.Activities;
using Genelife.Domain.Address;
using Genelife.Domain.CheatCodes;
using Genelife.Domain.Human.Activities;
using Genelife.Messages.Commands;
using Genelife.Messages.Commands.Grocery;
using Genelife.Messages.Commands.Locomotion;
using Genelife.Messages.Events;
using Genelife.Messages.Events.Buildings;
using Genelife.Messages.Events.Clock;
using Genelife.Messages.Events.Grocery;
using Genelife.Messages.Events.Locomotion;
using MassTransit;
using Serilog;

namespace Genelife.Application.Sagas;

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
    public Event<GroceryStoreAddressAnnounced>? GroceryStoreAddressAnnounced { get; set; } = null;
    public Event<AddGroceryStoreAddress>? AddGroceryStoreAddress { get; set; } = null;



    public HumanSaga()
    {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc =>
        {
            bc.Saga.Person = bc.Message.Person;
            Log.Information("Created human {HumanFirstName} {HumanLastName} ", bc.Saga.Person.FirstName, bc.Saga.Person.LastName);
            bc.Publish(new DiscoverGroceryStores(bc.Saga.CorrelationId));
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
        Event(() => FoodPurchased, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => DrinkPurchased, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => EnteredGroceryStore, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => LeftGroceryStore, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => GroceryStoreAddressAnnounced, e => e.CorrelateBy(saga => "any", _ => "any"));
        Event(() => AddGroceryStoreAddress, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        
        DuringAny(
            When(HourElapsed).Then(bc =>
            {
                bc.Saga.Person.Update();
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(SetAge).Then(bc =>
            {
                bc.Saga.Person.Execute(new ChangeBirthday(bc.Message.Value));
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(SetEnergy).Then(bc =>
            {
                bc.Saga.Person.Execute(new ChangeEnergy(bc.Message.Value));
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(SetHunger).Then(bc =>
            {
                bc.Saga.Person.Execute(new ChangeHunger(bc.Message.Value));
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(SetHygiene).Then(bc =>
            {
                bc.Saga.Person.Execute(new ChangeHygiene(bc.Message.Value));
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(SetMoney).Then(bc =>
            {
                bc.Saga.Person.Execute(new ChangeMoney(bc.Message.Value));
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(DayElapsed).Then(bc =>
            {
                bc.Saga.LastTime = bc.Message.DateTime;
                Log.Information($"{bc.Saga.CorrelationId} " +
                                $"needs: {Math.Round(bc.Saga.Person.Hunger)} hunger " +
                                $" {Math.Round(bc.Saga.Person.Energy)} energy " +
                                $" {Math.Round(bc.Saga.Person.Hygiene)} hygiene " +
                                $" {bc.Saga.Person.Money} money "
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
                bc.Saga.Person.AddressBook.AddHomeAddress(
                    bc.Message.Coordinates.X,
                    bc.Message.Coordinates.Y,
                    bc.Message.Coordinates.Z,
                    bc.Message.HomeId
                );
                Log.Information("home address added for human {SagaCorrelationId}", bc.Saga.CorrelationId);
            }),
            When(AddWorkAddress).Then(bc =>
            {
                bc.Saga.Person.AddressBook.AddWorkAddress(
                    bc.Message.OfficeLocation.X,
                    bc.Message.OfficeLocation.Y,
                    bc.Message.OfficeLocation.Z,
                    bc.Message.OfficeId
                );
                Log.Information("work address added for human {SagaCorrelationId}", bc.Saga.CorrelationId);
            }),
            When(Arrived).Then(bc => bc.Saga.Person.SetPosition(new Position(bc.Message.X, bc.Message.Y, bc.Message.Z))),
            When(GoToWork).Then(OnGoToWork),
            When(LeaveWork).Then(OnLeaveWork),
            When(GoHome).Then(bc => OnGoHome(bc.Saga.Person.AddressBook, bc, bc.Saga.CorrelationId)),
            When(AddMoney).Then(bc =>
            {
                bc.Saga.Person.Money += bc.Message.Amount;
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(FoodPurchased).Then(bc =>
            {
                bc.Saga.Person.BuyFood(1);
                Log.Information("Human {HumanId} purchased food. Total food items: {FoodCount}",
                    bc.Saga.CorrelationId, bc.Saga.Person.FoodItemCount);
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(DrinkPurchased).Then(bc =>
            {
                bc.Saga.Person.BuyDrink(1);
                Log.Information("Human {HumanId} purchased drink. Total drink items: {DrinkCount}",
                    bc.Saga.CorrelationId, bc.Saga.Person.DrinkItemCount);
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            When(GroceryStoreAddressAnnounced).Then(bc =>
            {
                bc.Saga.Person.AddressBook.AddGroceryStore(bc.Message.X, bc.Message.Y, bc.Message.Z, bc.Message.GroceryStoreId);
                Log.Information("Human {HumanId} learned about grocery store {StoreId} via announcement",
                    bc.Saga.CorrelationId, bc.Message.GroceryStoreId);
            }),
            When(AddGroceryStoreAddress).Then(bc =>
            {
                bc.Saga.Person.AddressBook.AddGroceryStore(bc.Message.X, bc.Message.Y, bc.Message.Z, bc.Message.GroceryStoreId);
                Log.Information("Human {HumanId} learned about grocery store {StoreId} via discovery",
                    bc.Saga.CorrelationId, bc.Message.GroceryStoreId);
            })
        );
        During(Idle, 
            When(UpdateTick).Then(bc =>
            {
                var activity = bc.Saga.Person.SelectNextActivity(bc.Message.DateTime, bc.Saga.HasJob);
                switch (activity)
                {
                    case Eat when bc.Saga.Person.FoodItemCount <= 0:
                    case Drink when bc.Saga.Person.FoodItemCount <= 0:
                    {
                        Log.Information("{sagaCorrelationId} doesnt have food or drink, going to the grocery store", bc.Saga.CorrelationId);
                        var storeId = bc.Saga.Person.AddressBook.NearestBuildingId(AddressType.Store, bc.Saga.Person.Coordinates);
                        if (storeId == Guid.Empty) break;
                        bc.Publish(new GoToGroceryStore(storeId, bc.Saga.CorrelationId));
                        bc.Saga.LastTime = DateTime.UtcNow;
                        bc.TransitionToState(Shopping);
                        return;
                    }
                }

                var state = activity switch
                {
                    Eat => Eating,
                    Drink => Drinking,
                    Sleep => Sleeping,
                    Shower => Showering,
                    Work => Working,
                    _ => Idle
                };
                bc.Saga.Activity = activity;
                bc.Saga.LastTime = bc.Message.DateTime;
                if (activity is Work)
                    bc.Publish(new GoToWork(bc.Saga.CorrelationId));
                bc.TransitionToState(state);
            })
        );

        During(Eating, Drinking, Sleeping, Showering,
            When(UpdateTick).Then(bc =>
            {
                if (!bc.Saga.Activity.IsCompleted(bc.Message.DateTime))
                    return;
                bc.Saga.LastTime = bc.Message.DateTime;
                bc.Saga.Person.Do(bc.Saga.Activity);
                Log.Information("{SagaCorrelationId} has finished {SagaCurrentState}", bc.Saga.CorrelationId, bc.Saga.CurrentState);
                bc.Saga.Activity = new Idle(bc.Message.DateTime);
                bc.TransitionToState(Idle);
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            Ignore(DayElapsed)
        );
        During(Working,
            When(UpdateTick).Then(bc =>
            {
                if (!bc.Saga.Activity.IsCompleted(bc.Message.DateTime))
                    return;
                bc.Saga.LastTime = bc.Message.DateTime;
                bc.Saga.Person.Do(bc.Saga.Activity);
                Log.Information("{SagaCorrelationId} has finished {SagaCurrentState}", bc.Saga.CorrelationId, bc.Saga.CurrentState);
                if (bc.Saga.Activity is Work)
                    bc.Publish(new LeaveWork(bc.Saga.CorrelationId));
                bc.Saga.Activity = new Idle(bc.Message.DateTime);
                bc.TransitionToState(Idle);
                bc.Publish(HumanUpdate.FromPerson(bc.Saga.Person, bc.Saga.CurrentState));
            }),
            Ignore(DayElapsed)
        );

        During(Shopping,
            When(EnteredGroceryStore).Then(bc =>
            {
                if (bc.Saga.Person.FoodItemCount <= 0)
                    bc.Publish(new BuyFood(bc.Message.GroceryStoreId, bc.Saga.CorrelationId));
                if (bc.Saga.Person.DrinkItemCount <= 0)
                    bc.Publish(new BuyDrink(bc.Message.GroceryStoreId, bc.Saga.CorrelationId));
                bc.Publish(new LeaveGroceryStore(bc.Message.GroceryStoreId, bc.Saga.CorrelationId));
            }),
            When(LeftGroceryStore).Then(bc =>
            {
                Log.Information("Human {HumanId} finished shopping and left grocery store", bc.Saga.CorrelationId);
                bc.Saga.Activity = new Idle(bc.Saga.LastTime);
            }).TransitionTo(Idle),
            Ignore(UpdateTick)
        );
    }

    private static void OnGoToWork(BehaviorContext<HumanSagaState, GoToWork> bc)
    {
        var workAddress = bc.Saga.Person.AddressBook.GetWorkAddress();
        var homeAddress = bc.Saga.Person.AddressBook.GetHomeAddress();
        bc.Publish(new LeaveHome(homeAddress.EntityId, bc.Saga.CorrelationId));
        bc.Publish(new EnteredWork(workAddress.EntityId, bc.Saga.CorrelationId));
        Log.Information("{SagaCorrelationId} is going to work at {WorkAddressEntityId}", bc.Saga.CorrelationId, workAddress.EntityId);
    }

    private static void OnLeaveWork(BehaviorContext<HumanSagaState, LeaveWork> bc)
    {
        var workAddress = bc.Saga.Person.AddressBook.GetWorkAddress();
        bc.Publish(new LeftWork(workAddress.EntityId, bc.Saga.CorrelationId));
        OnGoHome(bc.Saga.Person.AddressBook, bc, bc.Saga.CorrelationId);
        Log.Information("{SagaCorrelationId} is leaving work at {WorkAddressEntityId}", bc.Saga.CorrelationId, workAddress.EntityId);
    }

    private static void OnGoHome(AddressBook addressBook, IPublishEndpoint endpoint, Guid correlationId)
    {
        var homeAddress = addressBook.GetHomeAddress();
        endpoint.Publish(new GoHome(homeAddress.EntityId, correlationId));
    }
    
}
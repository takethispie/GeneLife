using System.Numerics;
using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using Genelife.Domain.Extensions;
using MassTransit;
using MongoDB.Bson;

namespace Genelife.Main.Sagas;

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State Idle { get; set; } = null!;
    public State Moving { get; set; } = null;
    public State GroceryStoreLoop { get; set; } = null;
    public State Working { get; set; } = null;

    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Arrived> Arrived { get; set; } = null;
    public Event<Tick> UpdateTick { get; set; } = null;
    public Event<DayElapsed> DayElapsed { get; set; } = null;
    public Event<ClosestGroceryShopFound> FoundGroceryShop { get; set; } = null;
    public Event<ItemsBought> ItemsBought { get; set; } = null;
    public Event<SetHunger> SetHunger { get; set; } = null;
    public Event<SetThirst> SetThirst { get; set; } = null;


    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            bc.Saga.Hunger = bc.Message.Hunger;
            bc.Saga.Thirst = bc.Message.Thirst;
            bc.Saga.Position = new Vector3(bc.Message.X, bc.Message.Y, 0);
            bc.Saga.Home = bc.Saga.Position;
        }).TransitionTo(Idle));
        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));

        DuringAny(
            When(DayElapsed).Then((bc) => {
                Console.WriteLine($"{bc.Saga.CorrelationId} Hunger: {bc.Saga.Hunger} Thirst: {bc.Saga.Thirst}");
                bc.Saga.Hunger++;
                bc.Saga.Thirst++;
            }),

            When(SetHunger).Then(bc => bc.Saga.Hunger = bc.Message.Value),
            When(SetThirst).Then(bc => bc.Saga.Thirst = bc.Message.Value)
        );

        AddIdleLoop();
        AddGroceryLoop();
        AddMovingLoop();
    }


#region gameplay loops
    private void AddIdleLoop() {

        During(Idle,
            When(UpdateTick, bc => bc.Saga.CurrentLoop == EventLoop.GroceryStore).TransitionTo(GroceryStoreLoop),

            When(UpdateTick, bc => ShouldEat(bc.Saga)).Then(bc => {
                var item = bc.Saga.Inventory.FirstOrDefault(x => x.ItemType == ItemType.Food);
                if(bc.Saga.Inventory.Remove(item) is false) throw new Exception("couldnt remove item");
                Console.WriteLine($"{bc.Saga.CorrelationId} Has Eaten");
                bc.Saga.Hunger = 0;
            }).TransitionTo(Idle),

            When(UpdateTick, bc => ShouldDrink(bc.Saga)).Then(bc => {
                var item = bc.Saga.Inventory.FirstOrDefault(x => x.ItemType == ItemType.Drink);
                if(bc.Saga.Inventory.Remove(item) is false) throw new Exception("couldnt remove item");
                Console.WriteLine($"{bc.Saga.CorrelationId} has Drank");
                bc.Saga.Thirst = 0;
            }).TransitionTo(Idle),

            When(UpdateTick, bc => ShouldGetGroceries(bc.Saga)).Then(async bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} Need to get groceries");
                bc.Saga.CurrentLoop = EventLoop.GroceryStore;
                bc.Saga.GroceryList = GetGroceryList(bc.Saga);
                await bc.Publish(new FindClosestGroceryShop(bc.Saga.CorrelationId, bc.Saga.Position));
            }).TransitionTo(GroceryStoreLoop),

            When(Arrived, bc => bc.Saga.CurrentLoop == EventLoop.GroceryStore).TransitionTo(GroceryStoreLoop),

            When(UpdateTick, bc => bc.Saga.Position != bc.Saga.Home).Then(bc => {
                bc.Saga.TargetId = Guid.Empty;
                bc.Saga.Target = bc.Saga.Home;
                Console.WriteLine("Going Home");
            }).TransitionTo(Moving)
        );
    }
    private void AddGroceryLoop() {
        During(GroceryStoreLoop,
            When(FoundGroceryShop).Then(bc => {
                bc.Saga.Target = new Vector3(bc.Message.X, bc.Message.Y, bc.Message.Z);
                bc.Saga.TargetId = bc.Message.GroceryShopId;
                Console.WriteLine($"{bc.Saga.CorrelationId} found grocery store at {bc.Message.X} {bc.Message.Y}");
            }).TransitionTo(Moving),

            When(UpdateTick, bc => IsNearbyTarget(bc.Saga))
                .Then(async bc => {
                    await bc.Publish(new BuyItems(bc.Saga.TargetId, bc.Saga.GroceryList, bc.Saga.CorrelationId));
                    Console.WriteLine($"{bc.Saga.CorrelationId} is buying items");
                }),
            
            When(ItemsBought).Then(bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} has bought items");
                bc.Saga.Inventory.AddRange(bc.Message.Items);
                bc.Saga.GroceryList = [];
                bc.Saga.Target = null;
                bc.Saga.TargetId = Guid.Empty;
                bc.Saga.CurrentLoop = EventLoop.Idle;
            }).TransitionTo(Idle)
        );
    }

    private void AddMovingLoop() {
        During(Moving,
            When(UpdateTick, bc => bc.Saga.Target.HasValue && Vector3.Distance(bc.Saga.Position, bc.Saga.Target.Value) > bc.Saga.Speed)
                .Then(bc => {
                    bc.Saga.Position = bc.Saga.Position.MovePointTowards(bc.Saga.Target.Value, bc.Saga.Speed);
                    Console.WriteLine($"{bc.Saga.CorrelationId} new position: {bc.Saga.Position}");
                }).TransitionTo(Moving),
            When(UpdateTick, bc => bc.Saga.Target.HasValue && Vector3.Distance(bc.Saga.Position, bc.Saga.Target.Value) <= bc.Saga.Speed)
                .Then(async bc => {
                    bc.Saga.Position = bc.Saga.Target.Value;
                    await bc.Publish(new Arrived(bc.Saga.CorrelationId, bc.Saga.TargetId));
                    Console.WriteLine($"{bc.Saga.CorrelationId} arrived at {bc.Saga.Target}");
                    bc.Saga.Target = null;
                    bc.Saga.TargetId = Guid.Empty;
                }).TransitionTo(Idle)
        );
    }
#endregion


#region loop activation logic
    private static bool ShouldGetGroceries(HumanSagaState saga) 
        => saga.Hunger >= 20 && !saga.Inventory.Any(x => x.ItemType == ItemType.Food)
            || saga.Thirst >= 10 && !saga.Inventory.Any(x => x.ItemType == ItemType.Drink);

    private static bool ShouldEat(HumanSagaState state)
        => state.Hunger >= 20 && state.Inventory.Any(x => x.ItemType == ItemType.Food);

    private static bool ShouldDrink(HumanSagaState state)
        => state.Thirst >= 10 && state.Inventory.Any(x => x.ItemType == ItemType.Drink);

    private static bool IsNearbyTarget(HumanSagaState state)
        => state.Target.HasValue && Vector3.Distance(state.Position, state.Target.Value) <= 2f && state.Target is not null;
#endregion


#region game logic
        public static GroceryListItem[] GetGroceryList(HumanSagaState state) => (state.Hunger >= 20, state.Thirst >= 10) switch {
            (true, false) => [new GroceryListItem(ItemType.Food, 1)],
            (false, true) => [new GroceryListItem(ItemType.Drink, 1)],
            (true, true) => [new GroceryListItem(ItemType.Drink, 1), new GroceryListItem(ItemType.Food, 1)],
            _ => []
        };
#endregion
}
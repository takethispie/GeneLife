using System.Numerics;
using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using Genelife.Domain.Extensions;
using MassTransit;

namespace Genelife.Main.Sagas;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public bool IsHome { get; set; } = true;
    public int Version { get; set; }
    public int Hunger { get; set; }
    public int Thirst { get; set; }
    public List<Item> Inventory { get; set; } = [];
    public Vector3 Position { get; set; }
    public Vector3? Target { get; set; } = null;
    public Guid TargetId { get; set; }
    public float Speed { get; set; }
    public EventLoop CurrentLoop { get; set; }
    public GroceryListItem[] GroceryList { get; set; } = [];
}

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State Idle { get; set; } = null!;
    public State Eating { get; set; } = null;
    public State Drinking { get; set; } = null;
    public State Moving { get; set; } = null;
    public State GroceryStoreLoop { get; set; } = null;

    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Arrived> Arrived { get; set; } = null;
    public Event<Tick> UpdateTick { get; set; } = null;
    public Event<ClosestGroceryShopFound> FoundGroceryShop { get; set; } = null;
    public Event<ItemBought> ItemBought { get; set; } = null;

    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).TransitionTo(Idle));

        AddIdleLoop();
        AddGroceryLoop();

        During(Moving,
            When(UpdateTick, bc => bc.Saga.Target.HasValue && Vector3.Distance(bc.Saga.Position, bc.Saga.Target.Value) > bc.Saga.Speed)
                .Then(bc => {
                    bc.Saga.Position = bc.Saga.Position.MovePointTowards(bc.Saga.Target.Value, bc.Saga.Speed);
                }).TransitionTo(Moving),
            When(UpdateTick, bc => bc.Saga.Target.HasValue && Vector3.Distance(bc.Saga.Position, bc.Saga.Target.Value) <= 2f)
                .Then(async bc => {
                    await bc.Publish(new Arrived(bc.Saga.CorrelationId, bc.Saga.TargetId));
                }).TransitionTo(Idle)
        );
    }


#region gameplay loops
    private void AddIdleLoop() {
        During(Idle,
            When(UpdateTick, bc => bc.Saga.CurrentLoop == EventLoop.GroceryStore).TransitionTo(GroceryStoreLoop),

            When(UpdateTick, bc => ShouldGetGroceries(bc.Saga)).Then(async bc => {
                bc.Saga.CurrentLoop = EventLoop.GroceryStore;
                bc.Saga.GroceryList = GetGroceryList(bc.Saga);
                await bc.Publish(new FindClosestGroceryShop(bc.Saga.CorrelationId, bc.Saga.Position));
            }).TransitionTo(GroceryStoreLoop),

            When(UpdateTick, bc => ShouldEat(bc.Saga)).Then(bc => {
                var item = bc.Saga.Inventory.FirstOrDefault(x => x.ItemType == ItemType.Food);
                if( bc.Saga.Inventory.Remove(item) is false) throw new Exception("couldnt remove item");
                bc.Saga.CurrentLoop = EventLoop.Idle;
            }).TransitionTo(GroceryStoreLoop),

            When(UpdateTick, bc => ShouldDrink(bc.Saga)).Then(bc => {
                var item = bc.Saga.Inventory.FirstOrDefault(x => x.ItemType == ItemType.Drink);
                if( bc.Saga.Inventory.Remove(item) is false) throw new Exception("couldnt remove item");
                bc.Saga.CurrentLoop = EventLoop.Idle;
            }).TransitionTo(GroceryStoreLoop)

        );
    }
    private void AddGroceryLoop() {
        During(GroceryStoreLoop,
            When(FoundGroceryShop).Then(bc => {
                bc.Saga.Target = bc.Message.Position;
                bc.Saga.TargetId = bc.Message.GroceryShopId;
            }).TransitionTo(Moving),

            When(UpdateTick, bc => IsNearby(bc.Saga))
                .Then(async bc => {
                    await bc.Publish(new BuyItems(bc.Saga.TargetId, bc.Saga.GroceryList, bc.Saga.CorrelationId));
                }),
            
            When(ItemBought).Then(bc => {
                bc.Saga.Inventory.Add(bc.Message.Item);
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

    private static bool IsNearby(HumanSagaState state)
        => state.Target.HasValue && Vector3.Distance(state.Position, state.Target.Value) <= 2f && state.Target is not null;
#endregion


#region game logic
        public static GroceryListItem[] GetGroceryList(HumanSagaState state) => (state.Hunger >= 20, state.Thirst >= 10) switch {
            (true, false) => [new GroceryListItem(ItemType.Drink, 1)],
            (false, true) => [new GroceryListItem(ItemType.Food, 1)],
            (true, true) => [new GroceryListItem(ItemType.Drink, 1), new GroceryListItem(ItemType.Food, 1)],
            _ => []
        };
#endregion
}
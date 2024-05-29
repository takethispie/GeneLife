using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Main.Sagas;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public bool IsHome { get; set; } = true;
    public int Version { get; set; }
    public bool NeedsFood { get; set; } = false;
    public bool NeedsDrink { get; set; } = false;
}

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State Idle { get; set; } = null!;
    public State Eating { get; set; } = null;
    public State Drinking { get; set; } = null;
    public State GettingGroceries { get; set; } = null;
    public State GoingHome { get; set; } = null;

    public Event<Dehydrated> Dehydrating { get; set; } = null;
    public Event<Starving> Starving { get; set; } = null;
    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Arrived> Arrived { get; set; } = null;
    public Event<ItemBought> ItemBought { get; set; } = null;
    public Event<HasDrank> Drank { get; set; } = null;
    public Event<HasEaten> Ate { get; set; } = null;

    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).TransitionTo(Idle));

        During(Idle,
            When(Dehydrating, bc => bc.Saga.NeedsDrink is false).Then(async bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} going to grocery shop");
                bc.Saga.NeedsDrink = true;
                await bc.Publish(new GoToGroceryShop(bc.Saga.CorrelationId));
            }).TransitionTo(GettingGroceries),

            When(Starving, bc => bc.Saga.NeedsFood is false).Then(async bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} going to grocery shop");
                bc.Saga.NeedsFood = true;
                //TODO doesnt work for some reason
                await bc.Publish(new GoToGroceryShop(bc.Saga.CorrelationId));
            }).TransitionTo(GettingGroceries), 

            When(Drank).Then(bc => {
                bc.Saga.NeedsDrink = false;
            }).TransitionTo(Idle),

            When(Ate).Then(bc => {
                bc.Saga.NeedsFood = false;
            }).TransitionTo(Idle),

            When(ItemBought).Then(DrinkOrEatWhenHasItem).TransitionTo(Idle)
        );

        During(GettingGroceries,
            When(Dehydrating).TransitionTo(GettingGroceries),
            When(Starving).TransitionTo(GettingGroceries),
            When(Arrived).Then(async bc => {
                bc.Saga.IsHome = false;
                await bc.Publish(new ListFoodAndDrinkToBuy(bc.Saga.CorrelationId, bc.Message.TargetId));
            }).TransitionTo(GettingGroceries),
            When(ItemBought).Then(DrinkOrEatWhenHasItem).TransitionTo(Idle)
        );

        During(GoingHome,
            When(Arrived).Then(bc => {
                Console.WriteLine($"{bc.Message.CorrelationId} got back home");
                bc.Saga.IsHome = true;
            }).TransitionTo(Idle)
        );
    }

    private async void DrinkOrEatWhenHasItem(BehaviorContext<HumanSagaState, ItemBought> bc) {
        if(bc.Saga.NeedsFood && bc.Message.ItemType == ItemType.Food) {
            await bc.Publish(new TakeItem(bc.Saga.CorrelationId, ItemType.Food));
            await bc.Publish(new Eat(bc.Saga.CorrelationId));
        } else if(bc.Saga.NeedsDrink && bc.Message.ItemType == ItemType.Drink) {
            await bc.Publish(new TakeItem(bc.Saga.CorrelationId, ItemType.Drink));
            await bc.Publish(new Drink(bc.Saga.CorrelationId));
        }
    }
}
using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Main.Sagas;

public class HumanSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public bool Starving { get; set; } = false;
    public bool Dehydrated { get; set; } = false;
    public int IdleTickTime { get; set; } = 0;
    public bool IsHome { get; set; } = true;
    public Guid Home { get; set; } = Guid.Empty;
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
    public Event<Arrived> ArrivedSomeWhere { get; set; } = null;
    public Event<ItemBought> ItemBought { get; set; } = null;
    public Event<HasDrank> Drank { get; set; } = null;
    public Event<HasEaten> Ate { get; set; } = null;
    public Event<Tick> Tick { get; set; } = null;

    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).TransitionTo(Idle));
        Event(() => Tick, e => e.CorrelateBy(saga => "any", ctx => "any"));

        During(Idle,
            When(Dehydrating).Then(async bc => {
                bc.Saga.Dehydrated = true;
                Console.WriteLine($"{bc.Saga.CorrelationId} going to grocery shop");
                await bc.Publish(new GoToGroceryShop(bc.Saga.CorrelationId));
            }).TransitionTo(GettingGroceries),

            When(Starving).Then(async bc => {
                bc.Saga.Starving = true;
                Console.WriteLine($"{bc.Saga.CorrelationId} going to grocery shop");
                await bc.Publish(new GoToGroceryShop(bc.Saga.CorrelationId));
            }).TransitionTo(GettingGroceries),

            When(Tick, (bc) => bc.Saga.IdleTickTime > 10 && bc.Saga.IsHome is false).Then(async bc => {
                bc.Saga.IdleTickTime++;
                bc.Saga.IdleTickTime = 0;
                await bc.Publish(new GoHome(bc.Saga.CorrelationId));
            }).TransitionTo(Idle),
            When(Tick, (bc) => bc.Saga.IsHome is false).Then(bc => bc.Saga.IdleTickTime++).TransitionTo(Idle),
            When(Tick).TransitionTo(Idle)
        );

        During(GettingGroceries,
            When(Dehydrating).Then(bc => bc.Saga.Dehydrated = true).TransitionTo(GettingGroceries),
            When(Starving).Then(bc => bc.Saga.Starving = true).TransitionTo(GettingGroceries),

            When(ArrivedSomeWhere).Then(async bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} arrived at {bc.Message.TargetId}");
                if(bc.Saga.Dehydrated) await bc.Publish(new BuyItem(bc.Saga.CorrelationId, ItemType.Drink, bc.Message.TargetId));
                if(bc.Saga.Starving) await bc.Publish(new BuyItem(bc.Saga.CorrelationId, ItemType.Food, bc.Message.TargetId));
            }).TransitionTo(GettingGroceries),

            When(ItemBought).Then(async bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} bought an item {bc.Message.ItemType}");
                var res = (bc.Saga.Dehydrated, bc.Saga.Starving, bc.Message.ItemType) switch
                {
                    (true, _, ItemType.Drink) => bc.Publish(new Drink(bc.Saga.CorrelationId)),
                    (_, true, ItemType.Food) => bc.Publish(new Eat(bc.Saga.CorrelationId)),
                   _ => Task.CompletedTask
                };
                await res;
            }).TransitionTo(GettingGroceries),

            When(Drank).Then(bc => bc.Saga.Dehydrated = false).TransitionTo(GettingGroceries),
            When(Ate).Then(bc => bc.Saga.Starving = false).TransitionTo(GettingGroceries),
            When(Tick, bc => bc.Saga.Dehydrated is false && bc.Saga.Starving is false).TransitionTo(Idle),
            When(Tick).TransitionTo(GettingGroceries)
        );

        During(GoingHome,
            When(ArrivedSomeWhere).Then(bc => bc.Saga.IsHome = true).TransitionTo(Idle),
            When(Tick).TransitionTo(GoingHome)
        );
    }
}
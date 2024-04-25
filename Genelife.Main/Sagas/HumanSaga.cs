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
}

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State Idle { get; set; } = null!;
    public State Eating { get; set; } = null;
    public State Drinking { get; set; } = null;
    public State GettingGroceries { get; set; } = null;


    public Event<Dehydrated> Dehydrating { get; set; } = null;
    public Event<Starving> Starving { get; set; } = null;
    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Arrived> ArrivedSomeWhere { get; set; } = null;

    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).TransitionTo(Idle));

        During(Idle,
            When(Dehydrating).Then(bc => {
                bc.Saga.Dehydrated = true;
                bc.Publish(new GoToGroceryShop(bc.Saga.CorrelationId));
            }).TransitionTo(GettingGroceries),

            When(Starving).Then(bc => {
                bc.Saga.Starving = true;
                bc.Publish(new GoToGroceryShop(bc.Saga.CorrelationId));
            }).TransitionTo(GettingGroceries)
        );

        During(GettingGroceries,
            When(ArrivedSomeWhere).Then(bc => {
                if(bc.Saga.Dehydrated) bc.Publish(new BuyItem(bc.Saga.CorrelationId, ItemType.Drink));
                if(bc.Saga.Starving) bc.Publish(new BuyItem(bc.Saga.CorrelationId, ItemType.Food));
            }).TransitionTo(GettingGroceries)
        );
    }
}
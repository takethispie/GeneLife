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
    public int Hunger { get; set; }
    public int Thirst { get; set; }
}

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State Idle { get; set; } = null!;
    public State Eating { get; set; } = null;
    public State Drinking { get; set; } = null;
    public State Moving { get; set; } = null;

    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Arrived> Arrived { get; set; } = null;
    public Event<Tick> UpdateTick { get; set; } = null;

    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).TransitionTo(Idle));

        During(Idle,
            When(UpdateTick, bc => bc.Saga.Hunger >= 20 || bc.Saga.Thirst >= 10).Then(async bc => {
                
            }).TransitionTo(Idle)
        );

        During(Moving,
            When(Arrived).TransitionTo(Idle)
        );
    }
}
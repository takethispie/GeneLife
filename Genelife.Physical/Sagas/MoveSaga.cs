using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using Genelife.Physical.Domain;
using MassTransit;
using System.Numerics;

namespace Genelife.Physical.Sagas;

public class MoveSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = null!;

    public Vector3 Position { get; set; }
    public Vector3 Target {  get; set; } = Vector3.Zero;

}

public class MoveSaga : MassTransitStateMachine<MoveSagaState>
{
    public State Idle { get; set; } = null!;
    public State Moving { get; set; } = null!;

    public Event<CreateHuman> CreateHuman { get; set; } = null!;
    public Event<MoveTo> MoveTo { get; set; } = null!;
    public Event<Tick> TickEvent { get; set; } = null!;

    public MoveSaga()
    {
        InstanceState(x => x.CurrentState);

        Initially(When(CreateHuman).Then(bc => bc.Saga.Position = bc.Message.Position).TransitionTo(Idle));

        During(Idle, 
            When(MoveTo)
            .Then(bc => {
                bc.Saga.Target = bc.Message.Position;
            }).TransitionTo(Moving)
        );

        During(Moving,
            When(TickEvent, ctx => Vector3.Distance(ctx.Saga.Position, ctx.Saga.Target) > 3)
            .Then(bc => {
                bc.Saga.Position.MovePointTowards(bc.Saga.Target, 100f);
            }).TransitionTo(Moving)
        );

        During(Moving,
            When(TickEvent, ctx => Vector3.Distance(ctx.Saga.Position, ctx.Saga.Target) <= 3)
            .Then(bc => {
                bc.Saga.Position = bc.Saga.Target;
                //fire arrived event
            }).TransitionTo(Idle)
        );
    }
}

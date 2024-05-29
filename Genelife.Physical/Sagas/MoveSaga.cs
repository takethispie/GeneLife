using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using Genelife.Physical.Extensions;
using MassTransit;
using System.Numerics;

namespace Genelife.Physical.Sagas;

public class MoveSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Target {  get; set; } = Vector3.Zero;
    public Guid TargetId { get; set; } = Guid.Empty;
    public int Version { get; set; }
}

public class MoveSaga : MassTransitStateMachine<MoveSagaState>
{
    public State Idle { get; set; }
    public State Moving { get; set; }
    public State Dead { get; set; }

    public Event<CreateHuman> CreateHuman { get; set; }
    public Event<MoveTo> MoveTo { get; set; }
    public Event<Tick> TickEvent { get; set; }
    public Event<Died> Died {  get; set; }

    public MoveSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => TickEvent, e => e.CorrelateBy(saga => "any", ctx => "any"));

        Initially(When(CreateHuman).Then(bc =>bc.Saga.Position = new Vector3(bc.Message.X, bc.Message.Y, 0)).TransitionTo(Idle));

        During(Idle,
            When(MoveTo)
            .Then(bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} moving to {bc.Message.X}:{bc.Message.Y}");
                bc.Saga.Target = new Vector3(bc.Message.X, bc.Message.Y, 0);
                bc.Saga.TargetId = bc.Message.TargetId;
            }).TransitionTo(Moving),

            When(TickEvent).TransitionTo(Idle),
            When(Died).TransitionTo(Dead)
        );

        During(Moving,
            When(TickEvent, ctx => Vector3.Distance(ctx.Saga.Position, ctx.Saga.Target) > 100f)
            .Then(bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} at {bc.Saga.Position} moving towards {bc.Saga.Target}");
                bc.Saga.Position = bc.Saga.Position.MovePointTowards(bc.Saga.Target, 100f);
            }).TransitionTo(Moving),

            When(Died).TransitionTo(Dead),
            When(MoveTo).Then(async bc => {
                await bc.Publish(new Busy(bc.Saga.CorrelationId));
            }).TransitionTo(Moving)
        );

        During(Moving,
            When(TickEvent, ctx => Vector3.Distance(ctx.Saga.Position, ctx.Saga.Target) <= 100f)
            .Then(async bc => {
                Console.WriteLine($"{bc.Saga.CorrelationId} arrived at {bc.Saga.Target}");
                bc.Saga.Position = bc.Saga.Target;
                await bc.Publish(new Arrived(bc.Saga.CorrelationId, bc.Saga.TargetId));
            }).TransitionTo(Idle),

            When(Died).TransitionTo(Dead)
        );
    }
}

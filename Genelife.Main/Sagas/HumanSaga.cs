using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Living;
using Genelife.Main.Domain.Activities;
using Genelife.Main.Usecases;
using MassTransit;
using Serilog;

namespace Genelife.Main.Sagas;

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State Idle { get; set; } = null!;
    public State Working { get; set; } = null;
    public State Sleeping { get; set; } = null!;
    public State Eating { get; set; } = null!;
    public State Showering { get; set; } = null!;

    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Tick> UpdateTick { get; set; } = null;
    public Event<DayElapsed> DayElapsed { get; set; } = null;
    public Event<HourElapsed> HourElapsed { get; set; } = null;


    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            bc.Saga.Human = bc.Message.Human;
        }).TransitionTo(Idle));
        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        
        DuringAny(
            When(HourElapsed).Then(bc => {
                bc.Saga.Human = new UpdateNeeds().Execute(bc.Saga.Human);
                Log.Information($"{bc.Saga.CorrelationId} " +
                    $"needs: {bc.Saga.Human.Hunger} hunger " +
                    $"and {bc.Saga.Human.Energy} energy " +
                    $"and {bc.Saga.Human.Hygiene} hygiene "
                );
            })
        );
        
        During(Idle, 
            When(UpdateTick).Then(bc => {
                if (bc.Saga.Activity != null) return;
                var activity = new ChooseActivity().Execute(bc.Saga.Human);
                var state = activity switch {
                    Eat eat => Eating,
                    Sleep sleep => Sleeping,
                    Shower shower => Showering,
                    _ => Idle
                };
                Log.Information($"{bc.Saga.CorrelationId} is transitioning to {state}");
                bc.TransitionToState(state);
            })
        );
        
        During(Eating,
            When(UpdateTick).Then(bc => {
                
            })    
        );
    }
}
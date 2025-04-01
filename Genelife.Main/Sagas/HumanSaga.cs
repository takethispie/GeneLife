using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Living;
using Genelife.Main.Domain;
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
    
    private readonly UpdateNeeds updateNeeds;
    private readonly ChooseActivity chooseActivity;


    public HumanSaga(UpdateNeeds updateNeedsUS, ChooseActivity chooseActivityUS) {
        updateNeeds = updateNeedsUS;
        chooseActivity = chooseActivityUS;
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            bc.Saga.Human = bc.Message.Human;
        }).TransitionTo(Idle));
        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => HourElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        
        DuringAny(
            When(HourElapsed).Then(bc => {
                bc.Saga.Human = updateNeeds.Execute(bc.Saga.Human);
                Log.Information($"{bc.Saga.CorrelationId} " +
                    $"needs: {bc.Saga.Human.Hunger} hunger " +
                    $"and {bc.Saga.Human.Energy} energy " +
                    $"and {bc.Saga.Human.Hygiene} hygiene "
                );
            })
        );
        
        During(Idle, 
            When(UpdateTick).Then(bc => {
                var activity = chooseActivity.Execute(bc.Saga.Human, bc.Message.Hour);
                var state = activity switch {
                    Eat eat => Eating,
                    Sleep sleep => Sleeping,
                    Shower shower => Showering,
                    _ => Idle
                };
                Log.Information($"{bc.Saga.CorrelationId} is transitioning to {state}");
                bc.Saga.Activity = activity.ToEnum();
                bc.Saga.ActivityTickDuration = activity.TickDuration;
                bc.Saga.Human = activity.Apply(bc.Saga.Human);
                bc.TransitionToState(state);
            }),
            When(DayElapsed).Then(bc => {})
        );
        
        During(Eating, Sleeping, Showering,
            When(UpdateTick).Then(bc => {
                if (bc.Saga.ActivityTickDuration >= 0) {
                    bc.Saga.ActivityTickDuration -= 1;
                    return;
                }
                Log.Information($"{bc.Saga.CorrelationId} has finished {bc.Saga.CurrentState}");
                bc.Saga.Activity = null;
                bc.TransitionToState(Idle);
            }),
            When(DayElapsed).Then(bc => {})
        );
    }
}
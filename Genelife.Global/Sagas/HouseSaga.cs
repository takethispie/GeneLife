using Genelife.Global.Messages.Events.Buildings;
using Genelife.Global.Sagas.States;
using MassTransit;
using Serilog;

namespace Genelife.Global.Sagas;

public class HouseSaga : MassTransitStateMachine<HouseSagaState> {

    public State Active { get; set; } = null!;

    public Event<HouseBuilt> Created { get; set; } = null!;

    public HouseSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            bc.Saga.Position = bc.Message.Location;
            if(bc.Message.Owners is not null)
                bc.Saga.Owners = bc.Message.Owners;
            Log.Information($"Created house {bc.Saga.CorrelationId} at {bc.Saga.Position.ToString()}");
        }).TransitionTo(Active));
    }

}
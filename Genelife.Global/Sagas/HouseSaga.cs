using System.Numerics;
using Genelife.Global.Messages.Commands.Locomotion;
using Genelife.Global.Messages.Events;
using Genelife.Global.Messages.Events.Buildings;
using Genelife.Global.Sagas.States;
using MassTransit;
using Serilog;

namespace Genelife.Global.Sagas;

public class HouseSaga : MassTransitStateMachine<HouseSagaState> {

    public State Active { get; set; } = null!;

    public Event<HouseBuilt> Created { get; set; } = null!;
    public Event<GoHome> HumanEntered { get; set; } = null!;
    public Event<LeaveHome> HumanLeft { get; set; } = null!;

    public HouseSaga() {
        InstanceState(x => x.CurrentState);
        
        Event(() => HumanEntered, 
            e => e.CorrelateById(x => x.Message.CorrelationId));
        Event(() => HumanLeft,
            e => e.CorrelateById(
                saga => saga.CorrelationId, 
                ctx => ctx.Message.CorrelationId
            )
        );
        
        Initially(When(Created).Then(bc =>
        {
            bc.Saga.Location = new Vector3(bc.Message.X, bc.Message.Y, bc.Message.Z);
            if(bc.Message.Owners is not null)
                bc.Saga.Owners = bc.Message.Owners;
            Log.Information($"Created house {bc.Saga.CorrelationId} at {bc.Saga.Location.ToString()}");
        }).TransitionTo(Active));
        
        During(Active,
            When(HumanEntered).Then(bc => {
                Log.Information($"Human {bc.Message.HumanId} is home");
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.HumanId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.HumanId];
                var pos = bc.Saga.Location;
                bc.Publish(new Arrived(bc.Message.HumanId,  pos.X, pos.Y, pos.Z, "Home"));
            }),
            
            When(HumanLeft).Then(bc => {
                Log.Information($"Human {bc.Message.HumanId} is leaving home");
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.HumanId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.HumanId];
            })
        );
    }
}
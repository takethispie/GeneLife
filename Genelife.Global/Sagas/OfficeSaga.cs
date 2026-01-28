using System.Numerics;
using Genelife.Global.Messages.Events.Buildings;
using Genelife.Global.Messages.Events.Locomotion;
using Genelife.Global.Sagas.States;
using MassTransit;
using Serilog;

namespace Genelife.Global.Sagas;

public class OfficeSaga : MassTransitStateMachine<OfficeSagaState> {

    public State Active { get; set; } = null!;

    public Event<OfficeCreated> Created { get; set; } = null!;
    public Event<EnteredWork> HumanEntered { get; set; } = null!;
    public Event<LeftWork> HumanLeft { get; set; } = null!;

    public OfficeSaga() {
        InstanceState(x => x.CurrentState);
        
        Event(() => HumanEntered,
            e => e.CorrelateById(
                saga => saga.CorrelationId, 
                ctx => ctx.Message.CorrelationId
            )
        );
        
        Event(() => HumanLeft,
            e => e.CorrelateById(
                saga => saga.CorrelationId, 
                ctx => ctx.Message.CorrelationId
            )
        );
        
        Initially(When(Created).Then(bc => {
            bc.Saga.Location = new Vector3(bc.Message.X, bc.Message.Y, bc.Message.Z);
            bc.Saga.Name = bc.Message.Name;
            bc.Saga.OwningCompanyId = bc.Message.OwningCompanyId;
            Log.Information($"Created office {bc.Saga.CorrelationId} at {bc.Saga.Location.ToString()}");
        }).TransitionTo(Active));
        
        During(Active,
            When(HumanEntered).Then(bc => {
                Log.Information($"Human {bc.Message.BeingId} is at {bc.Saga.OwningCompanyId} office");
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.BeingId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.BeingId];
                var pos = bc.Saga.Location;
                bc.Publish(new Arrived(bc.Message.BeingId,  pos.X, pos.Y, pos.Z, "Work"));
            }),
            
            When(HumanLeft).Then(bc => {
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.BeingId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.BeingId];
                Log.Information($"Human {bc.Message.BeingId} is leaving {bc.Saga.OwningCompanyId} office");
            })
        );
    }
}
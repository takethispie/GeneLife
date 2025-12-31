using System.Numerics;
using Genelife.Global.Extensions;
using Genelife.Global.Messages.Commands.Locomotion;
using Genelife.Global.Messages.Events.Buildings;
using Genelife.Global.Messages.Events.Locomotion;
using Genelife.Global.Sagas.States;
using MassTransit;
using Serilog;

namespace Genelife.Global.Sagas;

public class OfficeSaga : MassTransitStateMachine<OfficeSagaState> {

    public State Active { get; set; } = null!;

    public Event<OfficeCreated> Created { get; set; } = null!;
    public Event<GoToWork> HumanEntered { get; set; } = null!;
    public Event<LeaveWork> HumanLeft { get; set; } = null!;

    public OfficeSaga() {
        InstanceState(x => x.CurrentState);
        
        Event(() => HumanEntered,
            e => e.CorrelateById(
                saga => saga.OwningCompanyId, 
                ctx => ctx.Message.OwningCompanyId
            )
        );
        
        Event(() => HumanLeft,
            e => e.CorrelateById(
                saga => saga.OwningCompanyId, 
                ctx => ctx.Message.OwningCompanyId
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
                Log.Information($"Human {bc.Message.HumanId} is at {bc.Saga.OwningCompanyId} office");
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.HumanId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.HumanId];
                var pos = bc.Saga.Location;
                bc.Publish(new Arrived(bc.Message.HumanId,  pos.X, pos.Y, pos.Z, "Work"));
            }),
            
            When(HumanLeft).Then(bc => {
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.HumanId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.HumanId];
                Log.Information($"Human {bc.Message.HumanId} is leaving {bc.Saga.OwningCompanyId} office");
            })
        );
    }
}
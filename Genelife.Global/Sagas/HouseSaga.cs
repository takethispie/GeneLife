using System.Numerics;
using Genelife.Global.Messages.Commands.Locomotion;
using Genelife.Global.Messages.Events;
using Genelife.Global.Messages.Events.Buildings;
using Genelife.Global.Messages.Events.Locomotion;
using Genelife.Global.Sagas.States;
using Genelife.Life.Messages.Commands;
using Genelife.Life.Messages.DTOs;
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
            e => e.CorrelateById(
                saga => saga.CorrelationId, 
                x => x.Message.CorrelationId)
        );
        Event(() => HumanLeft,
            e => e.CorrelateById(
                saga => saga.CorrelationId,
                ctx => ctx.Message.CorrelationId
            )
        );
        
        Initially(When(Created).Then(bc =>
        {
            bc.Saga.Position = new (bc.Message.X, bc.Message.Y, bc.Message.Z);
            if (bc.Message.Owners is not null)
            {
                bc.Saga.Owners = bc.Message.Owners;
                bc.Message.Owners.ForEach(owner =>
                {
                    bc.Publish(new SetHomeAddress(
                        owner, 
                        bc.Saga.CorrelationId, 
                        new Coordinates(bc.Message.X, bc.Message.Y, bc.Message.Z)
                    ));
                });
            }
            Log.Information($"Created house {bc.Saga.CorrelationId} at {bc.Saga.Position}");
        }).TransitionTo(Active));
        
        During(Active,
            When(HumanEntered).Then(bc => {
                Log.Information($"Human {bc.Message.HumanId} is home");
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.HumanId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.HumanId];
                var pos = bc.Saga.Position;
                bc.Publish(new Arrived(bc.Message.HumanId,  pos.X, pos.Y, pos.Z, "Home"));
            }),
            
            When(HumanLeft).Then(bc => {
                Log.Information($"Human {bc.Message.HumanId} is leaving home");
                bc.Saga.Occupants = bc.Saga.Occupants.Exists(occupant => occupant == bc.Message.HumanId)
                    ? bc.Saga.Occupants
                    : [..bc.Saga.Occupants, bc.Message.HumanId];
                bc.Publish(new LeftHome(bc.Saga.CorrelationId,  bc.Message.HumanId));
            })
        );
    }
}
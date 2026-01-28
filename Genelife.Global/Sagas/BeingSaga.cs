using System.Numerics;
using Genelife.Global.Domain.Address;
using Genelife.Global.Domain.Exceptions;
using Genelife.Global.Messages.Commands;
using Genelife.Global.Messages.Commands.Locomotion;
using Genelife.Global.Messages.DTOs;
using Genelife.Global.Messages.Events.Locomotion;
using Genelife.Global.Sagas.States;
using MassTransit;

namespace Genelife.Global.Sagas;

public class BeingSaga : MassTransitStateMachine<BeingSagaState>
{
    public State Active { get; set; } = null!;
    
    public Event<AddHomeAddress> AddHomeAddress { get; set; } = null!;
    public Event<AddWorkAddress> AddWorkAddress { get; set; } = null!;
    public Event<LeaveWork> LeaveWork { get; set; } = null!;
    public Event<LeaveHome> LeaveHome { get; set; } = null!;
    public Event<GoToWork> GoToWork { get; set; } = null!;
    public Event<GoHome> GoHome { get; set; } = null!;
    public Event<CreateBeingLocation> CreateBeingLocation { get; set; } = null!;    
    public Event<Arrived> Arrived { get; set; } = null!;
    
    public BeingSaga()
    {
        InstanceState(x => x.CurrentState);
        Initially(
            When(CreateBeingLocation).Then(bc =>
            {
                bc.Saga.CorrelationId = bc.Message.CorrelationId;
                bc.Saga.HumanId = bc.Message.HumanId;
                bc.Saga.Position = new Position(new Vector3(bc.Message.X, bc.Message.Y, bc.Message.Z), "");
                bc.Saga.AddressBook =  new AddressBook();
            }).TransitionTo(Active)
        );
        Event(() => AddHomeAddress, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => AddWorkAddress, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));
        Event(() => Arrived, e => e.CorrelateById(saga => saga.CorrelationId, ctx => ctx.Message.CorrelationId));

        During(Active,
            When(AddHomeAddress).Then(bc =>
            {
                var entry = new AddressEntry(
                    new Position(bc.Message.Location, "Home"),
                    AddressType.Home,
                    Guid.Empty
                );
                bc.Saga.AddressBook.Add(entry);
            }),
            When(AddWorkAddress).Then(bc =>
            {
                var entry = new AddressEntry(
                    new Position(bc.Message.Location, "Work"),
                    AddressType.Office,
                    bc.Message.OfficeId
                );
                bc.Saga.AddressBook.Add(entry);
            }),
            When(Arrived) .Then(bc => bc.Saga.Position = new Position(
                new Vector3(bc.Message.X, bc.Message.Y, bc.Message.Z), bc.Message.LocationName)
            ),
            When(GoToWork).Then(OnGoToWork),
            When(LeaveWork).Then(OnLeaveWork),
            When(GoHome).Then(bc => OnGoHome(bc.Saga.AddressBook, bc, bc.Saga.CorrelationId))
        );
    }
    
    private static void OnGoToWork(BehaviorContext<BeingSagaState, GoToWork> bc)
    {
        var workAddress = bc.Saga.AddressBook.AllOfAddressType(AddressType.Office).FirstOrDefault();
        //TODO use fallback system-wide event with just the human correlationId and no target office
        if (workAddress is null) 
            throw new AddressNotFoundException(nameof(workAddress));
        bc.Publish(new EnteredWork(bc.Saga.CorrelationId, workAddress.EntityId));
    }

    private static void OnLeaveWork(BehaviorContext<BeingSagaState, LeaveWork> bc)
    {
        var workAddress = bc.Saga.AddressBook.AllOfAddressType(AddressType.Office).FirstOrDefault();
        //TODO use fallback system-wide event with just the human correlationId and no target office
        if (workAddress is null) 
            throw new AddressNotFoundException(nameof(workAddress));
        bc.Publish(new LeftWork(workAddress.EntityId, bc.Saga.CorrelationId));
        OnGoHome(bc.Saga.AddressBook, bc, bc.Saga.CorrelationId);
    }

    private static void OnGoHome(AddressBook addressBook, IPublishEndpoint endpoint, Guid correlationId)
    {
        var homeAddress = addressBook
            .AllOfAddressType(AddressType.Home)
            .FirstOrDefault(x => x.Position.LocationLabel == "Home");
        if (homeAddress is null) 
            throw new AddressNotFoundException(nameof(homeAddress));
        var pos = homeAddress.Position.Location;
        endpoint.Publish(new Arrived(correlationId, pos.X, pos.Y, pos.Z, "Home"));
    }
}
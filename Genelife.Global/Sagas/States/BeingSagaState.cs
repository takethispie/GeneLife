using System.Numerics;
using Genelife.Global.Domain.Address;
using Genelife.Global.Messages.DTOs;
using MassTransit;

namespace Genelife.Global.Sagas.States;

public class BeingSagaState : SagaStateMachineInstance, ISagaVersion {
    public Guid CorrelationId { get; set; }
    public Guid HumanId { get; set; }
    public AddressBook AddressBook { get; set; } = null!;
    public Position Position { get; set; } = null!;
    public int Version { get; set; }
    public string CurrentState { get; set; } = null!;
}
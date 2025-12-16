using Genelife.Life.Domain.Address;
using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;
using MassTransit;

namespace Genelife.Life.Sagas.States;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Human Human { get; set; } = null!;
    public AddressBook AddressBook { get; set; }
    public ILivingActivity? Activity { get; set; }
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
    public bool HasJob { get; set; }
}

using Genelife.Life.Domain.Activities;
using Genelife.Life.Domain.Address;
using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;
using MassTransit;
using Position = Genelife.Global.Messages.DTOs.Position;

namespace Genelife.Life.Sagas.States;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Human Human { get; set; } = null!;
    public ILivingActivity Activity { get; set; } = new Idle();
    public AddressBook AddressBook { get; set; } = null!;
    public Position Position { get; set; } = new (0, 0, 0);
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
    public bool HasJob { get; set; }
}

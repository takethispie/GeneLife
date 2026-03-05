using Genelife.Domain;
using Genelife.Domain.Activities;
using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Address;
using Genelife.Domain.Human;
using MassTransit;
using Position = Genelife.Domain.Position;

namespace Genelife.Application.Sagas.States;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Person Person { get; set; } = null!;
    public IBeingActivity Activity { get; set; } = new Idle();
    public AddressBook AddressBook { get; set; } = null!;
    public Position Position { get; set; } = new (0, 0, 0);
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
    public bool HasJob { get; set; }
    public int FoodCount { get; set; } = 0;
    public int DrinkCount { get; set; } = 0;
}

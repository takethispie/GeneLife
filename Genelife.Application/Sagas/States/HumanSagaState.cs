using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;
using MassTransit;

namespace Genelife.Application.Sagas.States;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Person Person { get; set; } = null!;
    public IBeingActivity Activity { get; set; } = null!;
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
    public bool HasJob { get; set; }
    public DateTime LastTime { get; set; }
}

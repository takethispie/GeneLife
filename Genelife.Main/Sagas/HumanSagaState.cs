using Genelife.Domain;
using Genelife.Main.Domain;
using MassTransit;

namespace Genelife.Main.Sagas;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Human Human { get; set; } = null!;
    public Employment EmploymentProfile { get; set; }
    public ActivityEnum? Activity { get; set; }
    public string CurrentState { get; set; }
    public int Version { get; set; }
    public int ActivityTickDuration { get; set; }
}

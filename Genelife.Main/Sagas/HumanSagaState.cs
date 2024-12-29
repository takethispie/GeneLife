using System.Numerics;
using Genelife.Domain;
using Genelife.Domain.Interfaces;
using MassTransit;

namespace Genelife.Main.Sagas;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Human Human { get; set; } = null!;
    public ActivityEnum? Activity { get; set; }
    public string CurrentState { get; set; }
    public int Version { get; set; }
    public int ActivityTickDuration { get; set; }
}

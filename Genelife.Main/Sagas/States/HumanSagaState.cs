using Genelife.Domain;
using Genelife.Domain.Interfaces;
using Genelife.Domain.Work;
using Genelife.Main.Domain;
using MassTransit;

namespace Genelife.Main.Sagas.States;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Human Human { get; set; } = null!;
    public Employment Employment { get; set; } = null!;
    public ILivingActivity? Activity { get; set; }
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
}

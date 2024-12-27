using System.Numerics;
using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Commands.Items;
using Genelife.Domain.Human;
using Genelife.Domain.Items;
using MassTransit;

namespace Genelife.Main.Sagas;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Character Character { get; set; }
    public string CurrentState { get; set; }
    public int Version { get; set; }
    public Vector3 Position { get; set; }
    public Vector3? Target { get; set; } = null;
    public Vector3? Home { get; set; } = null;
    public float Speed { get; set; } = 100f;
}

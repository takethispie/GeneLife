using System.Numerics;
using Genelife.Domain;
using Genelife.Domain.Commands;
using MassTransit;

namespace Genelife.Main.Sagas;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public int Version { get; set; }
    public int Hunger { get; set; } = 0;
    public int Thirst { get; set; } = 0;
    public List<Item> Inventory { get; set; } = [];
    public Vector3 Position { get; set; }
    public Vector3? Target { get; set; } = null;
    public Guid TargetId { get; set; }
    public float Speed { get; set; }
    public EventLoop CurrentLoop { get; set; } = EventLoop.Idle;
    public GroceryListItem[] GroceryList { get; set; } = [];
}

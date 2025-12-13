using System.Numerics;
using MassTransit;

namespace Genelife.Global.Sagas.States;

public class HouseSagaState : SagaStateMachineInstance, ISagaVersion {
    public Guid CorrelationId { get; set; }
    public Vector3 Position { get; set; } = Vector3.Zero;
    public List<Guid> Owners { get; set; } = [];
    public int Version { get; set; }
    public string CurrentState { get; set; } = null!;
}
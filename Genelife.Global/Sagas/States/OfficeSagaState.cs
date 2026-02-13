using System.Numerics;
using MassTransit;

namespace Genelife.Global.Sagas.States;

public class OfficeSagaState: SagaStateMachineInstance, ISagaVersion {
    public Guid CorrelationId { get; set; }
    public Vector3 Location { get; set; } = Vector3.Zero;
    public Guid OwningCompanyId { get; set; }
    public List<Guid> Occupants { get; set; } = [];
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string CurrentState { get; set; } = null!;
}
using System.Numerics;
using Genelife.Global.Messages.DTOs;
using MassTransit;

namespace Genelife.Global.Sagas.States;

public class HouseSagaState : SagaStateMachineInstance, ISagaVersion {
    public Guid CorrelationId { get; set; }
    public Position Position { get; set; } = new(0, 0, 0);
    public List<Guid> Owners { get; set; } = [];
    public List<Guid> Occupants { get; set; } = [];
    public int Version { get; set; }
    public string CurrentState { get; set; } = null!;
}
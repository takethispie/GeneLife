using Genelife.Work.Messages.DTOs.Skills;
using MassTransit;

namespace Genelife.Work.Sagas.States;

public record WorkerSagaState() : SagaStateMachineInstance, ISagaVersion {
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
    public string LastName { get; set; }
    public int YearsOfExperience { get; set; }
    public SkillSet SkillSet { get; set; }
    public string FirstName { get; set; }
}
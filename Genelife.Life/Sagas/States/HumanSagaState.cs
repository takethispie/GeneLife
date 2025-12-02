using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;
using MassTransit;

namespace Genelife.Life.Sagas.States;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Human Human { get; set; } = null!;
    public bool SeekingJob { get; set; } = false;
    public Guid EmployerId { get; set; } = Guid.Empty;
    public SkillSet SkillSet { get; set; } = new();
    public int? HiringTimeOut { get; set; } = null;
    public int YearsOfExperience { get; set; } = 0;
    public ILivingActivity? Activity { get; set; }
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
}

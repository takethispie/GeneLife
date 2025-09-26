using Genelife.Domain;
using Genelife.Domain.Interfaces;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Skills;
using Genelife.Main.Domain;
using MassTransit;

namespace Genelife.Main.Sagas.States;

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

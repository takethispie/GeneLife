using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;
using Genelife.Domain.Work.Skills;
using MassTransit;

namespace Genelife.Application.Sagas.States;

public class HumanSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Person Person { get; set; } = null!;
    public IBeingActivity Activity { get; set; } = null!;
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
    public bool HasJob { get; set; }
    public DateTime LastTime { get; set; }

    // Worker fields
    public SkillSet SkillSet { get; set; } = new();
    public int YearsOfExperience { get; set; }
    public int? HiringTimeOut { get; set; }
    public Guid EmployerId { get; set; }
    public bool IsLookingForJob { get; set; }
}

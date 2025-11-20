using Genelife.Work.Messages.DTOs;
using MassTransit;

namespace Genelife.Work.Sagas.States;

public class JobPostingSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public JobPosting JobPosting { get; set; } = null!;
    public List<JobApplication> Applications { get; set; } = [];
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
    public int DaysActive { get; set; }
}
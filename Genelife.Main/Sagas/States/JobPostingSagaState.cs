using Genelife.Domain.Work;
using Genelife.Main.Domain;
using MassTransit;

namespace Genelife.Main.Sagas.States;

public class JobPostingSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public JobPosting JobPosting { get; set; } = null!;
    public List<IdentifiedJobApplication> Applications { get; set; } = [];
    public string CurrentState { get; set; } = null!;
    public int Version { get; set; }
    public DateTime CreatedDate { get; set; }
    public int DaysActive { get; set; }
    public int ApplicationsReceived { get; set; }
    public Guid SelectedApplicationId { get; set; }
}
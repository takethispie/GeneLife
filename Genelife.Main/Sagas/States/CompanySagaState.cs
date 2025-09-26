using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Main.Sagas.States;

public class CompanySagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Company Company { get; set; } = null!;
    public List<Employee> Employees { get; set; } = [];
    public int DaysElapsedCount { get; set; } 
    public int? PublishedJobPostings { get; set; } = null;
    public string CurrentState { get; set; }
    
    public int Version { get; set; }
    public DateTime LastPayrollDate { get; set; }
    public float AverageProductivity { get; set; } = 1.0f;
}
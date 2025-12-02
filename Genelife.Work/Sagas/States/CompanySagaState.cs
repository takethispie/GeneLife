using Genelife.Work.Messages.DTOs;
using MassTransit;

namespace Genelife.Work.Sagas.States;

public class CompanySagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Company Company { get; set; } = null!;
    public List<Employee> Employees { get; set; } = [];
    public int DaysElapsedCount { get; set; } 
    public int? PublishedJobPostings { get; set; }
    public string CurrentState { get; set; }
    
    public int Version { get; set; }
    public DateTime LastPayrollDate { get; set; }
}
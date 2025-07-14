using Genelife.Domain;
using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Main.Sagas;

public class CompanySagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Company Company { get; set; } = null!;
    public List<Employee> Employees { get; set; } = new();
    public int DaysElapsedCount { get; set; }
    public PayrollState PayrollState { get; set; }
    public HiringState HiringState { get; set; }
    public WorkProgressState WorkProgressState { get; set; }
    public string CurrentState { get; set; }
    
    public int Version { get; set; }
    public DateTime LastPayrollDate { get; set; }
    public int PositionsNeeded { get; set; }
    public decimal AverageProductivity { get; set; } = 1.0m;
}
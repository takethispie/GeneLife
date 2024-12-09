namespace Genelife.Domain.Scheduler;

public interface ISlot {
    public TimeOnly Start { get; set; }
}
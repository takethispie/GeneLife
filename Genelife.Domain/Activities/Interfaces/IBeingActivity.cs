using Genelife.Domain.Human;

namespace Genelife.Domain.Activities.Interfaces;

public interface IBeingActivity {
    public DateTime StartTime { get; }
    public TimeSpan Duration { get; }
    
    public bool IsCompleted(DateTime currentTime) => currentTime >= StartTime.Add(Duration);
}
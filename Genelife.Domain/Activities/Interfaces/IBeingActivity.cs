using Genelife.Domain.Human;

namespace Genelife.Domain.Activities.Interfaces;

public interface IBeingActivity {
    public TimeSpan Duration { get; set; }
}
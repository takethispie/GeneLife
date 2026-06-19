using Genelife.Enums;

namespace Genelife.Components;

/// <summary>
/// Component representing the current action a Sim is performing
/// </summary>
public record struct CurrentAction
{
    public ActionType Type;
    public float TimeRemaining;
    
    public CurrentAction(ActionType type, float duration)
    {
        Type = type;
        TimeRemaining = duration;
    }
}

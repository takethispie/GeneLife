using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// System that makes decisions for Sims based on their needs
/// </summary>
public class DecisionSystem
{
    private readonly World _world;
    private readonly QueryDescription _queryDescription;
    private const float CriticalThreshold = 30f;

    public DecisionSystem(World world)
    {
        _world = world;
        _queryDescription = new QueryDescription().WithAll<CurrentAction, Needs>();
    }

    public void Update()
    {
        _world.Query(in _queryDescription, (ref CurrentAction action, ref Needs needs) =>
        {
            // Only make decisions when idle
            if (action.Type != ActionType.Idle)
                return;

            // Find most critical need
            var lowestNeed = GetLowestNeed(needs);

            if (lowestNeed.value < CriticalThreshold)
            {
                action.Type = lowestNeed.actionType;
                action.TimeRemaining = lowestNeed.duration;
            }
        });
    }

    private (float value, ActionType actionType, float duration) GetLowestNeed(Needs needs)
    {
        var needsList = new[]
        {
            (needs.Hunger, ActionType.Eating, 5f),
            (needs.Energy, ActionType.Sleeping, 8f),
            (needs.Hygiene, ActionType.Showering, 3f),
            (needs.Bladder, ActionType.UsingToilet, 2f)
        };

        return needsList.OrderBy(n => n.Item1).First();
    }
}

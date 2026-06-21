using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.Systems;

public class DecisionSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;
    private const float CriticalThreshold = 30f;

    public DecisionSystem(World world)
    {
        this.world = world;
        queryDescription = new QueryDescription().WithAll<CurrentAction, Needs>();
    }

    public void Update()
    {
        world.Query(in queryDescription, (ref CurrentAction action, ref Needs needs) =>
        {
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

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// System that handles action execution and need fulfillment
/// </summary>
public class ActionSystem
{
    private readonly World _world;
    private readonly QueryDescription _queryDescription;

    public ActionSystem(World world)
    {
        _world = world;
        _queryDescription = new QueryDescription().WithAll<CurrentAction, Needs>();
    }

    public void Update(float deltaTime)
    {
        _world.Query(in _queryDescription, (ref CurrentAction action, ref Needs needs) =>
        {
            action.TimeRemaining -= deltaTime;

            // Apply action effects while performing
            switch (action.Type)
            {
                case ActionType.Sleeping:
                    needs.Energy = Math.Min(100f, needs.Energy + 5f * deltaTime);
                    break;
                case ActionType.Showering:
                    needs.Hygiene = Math.Min(100f, needs.Hygiene + 10f * deltaTime);
                    break;
                case ActionType.Eating:
                    needs.Hunger = Math.Min(100f, needs.Hunger + 8f * deltaTime);
                    break;
                case ActionType.UsingToilet:
                    needs.Bladder = Math.Min(100f, needs.Bladder + 15f * deltaTime);
                    break;
            }

            // Action completed
            if (action.TimeRemaining <= 0)
            {
                action.Type = ActionType.Idle;
                action.TimeRemaining = 0;
            }
        });
    }
}

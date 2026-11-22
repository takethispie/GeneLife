using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// System that handles action execution and need fulfillment
/// </summary>
public class ActionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;

    public ActionSystem(World world)
    {
        this.world = world;
        queryDescription = new QueryDescription().WithAll<CurrentAction, Needs>();
    }

    public string Name => "Action";
    public int Order => 300;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.DeltaTime);

    public void Update(float deltaTime)
    {
        world.Query(in queryDescription, (Entity entity, ref CurrentAction action, ref Needs needs) =>
        {
            action.TimeRemaining -= deltaTime;

            var canFulfill = CanFulfill(entity, action.Type);

            // Apply action effects while performing. Eating and hygiene require a
            // household purchase for sims that have a ConsumptionState component;
            // legacy/test sims without one keep the free-restoration behaviour.
            if (canFulfill)
            {
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
                    case ActionType.Idle:
                    default:
                        break;
                }
            }

            // Action completed
            if (action.TimeRemaining > 0) return;

            // Fulfill the targeted need completely once the action finishes
            if (canFulfill)
            {
                switch (action.Type)
                {
                    case ActionType.Sleeping:
                        needs.Energy = 100f;
                        break;
                    case ActionType.Showering:
                        needs.Hygiene = 100f;
                        break;
                    case ActionType.Eating:
                        needs.Hunger = 100f;
                        break;
                    case ActionType.UsingToilet:
                        needs.Bladder = 100f;
                        break;
                }
            }

            action.Type = ActionType.Idle;
            action.TimeRemaining = 0;
        });
    }

    private bool CanFulfill(Entity entity, ActionType action)
    {
        if (!world.Has<ConsumptionState>(entity))
            return true;

        var state = world.Get<ConsumptionState>(entity);
        return action switch
        {
            ActionType.Eating => state.FoodPurchased,
            ActionType.Showering => state.HygienePurchased,
            _ => true
        };
    }
}

using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;

namespace Genelife.Systems;

public class NeedsDecaySystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;

    public NeedsDecaySystem(World world)
    {
        this.world = world;
        queryDescription = new QueryDescription().WithAll<Needs>();
    }

    public void Update(float deltaTime)
    {
        world.Query(in queryDescription, (ref Needs needs) =>
        {
            // Decay rates per second
            needs.Hunger -= 0.5f * deltaTime;
            needs.Energy -= 0.3f * deltaTime;
            needs.Hygiene -= 0.2f * deltaTime;
            needs.Bladder -= 0.4f * deltaTime;

            needs.Hunger = Math.Max(0f, needs.Hunger);
            needs.Energy = Math.Max(0f, needs.Energy);
            needs.Hygiene = Math.Max(0f, needs.Hygiene);
            needs.Bladder = Math.Max(0f, needs.Bladder);
        });
    }
}

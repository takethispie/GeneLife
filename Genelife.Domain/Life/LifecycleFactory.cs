using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Life;

/// <summary>
/// Creates lifecycle state with deterministic birthday phases so births are
/// spread across the in-game year.
/// </summary>
public static class LifecycleFactory
{
    public static Lifecycle Create(
        int age,
        long tick,
        SimulationClock clock,
        LifeStagePolicy? policy = null,
        int phaseSeed = 0)
    {
        policy ??= LifeStagePolicy.Default;
        var clampedAge = Math.Clamp(age, 0, policy.MaxAge);
        var offset = BirthdayOffset(phaseSeed, clock.TicksPerYear);
        return new Lifecycle(
            policy.StageFor(clampedAge),
            (long)clampedAge * clock.TicksPerYear + offset,
            tick,
            StageTransitionCause.Restored,
            tick);
    }

    public static int BirthdayOffset(int seed, int ticksPerYear)
    {
        if (ticksPerYear <= 1)
            return 0;
        var hash = unchecked((uint)(seed * 2654435761));
        return (int)(hash % (uint)(ticksPerYear - 1));
    }
}

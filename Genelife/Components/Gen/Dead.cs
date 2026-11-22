using Genelife.Enums;

namespace Genelife.Components.Gen;

/// <summary>
/// Death marker. Every gameplay query excludes dead sims; the entity itself is
/// destroyed one snapshot tick after its relationships and estate are settled.
/// </summary>
public record struct Dead
{
    public long Tick;
    public DeathCause Cause;

    public Dead(long tick, DeathCause cause)
    {
        Tick = tick;
        Cause = cause;
    }
}

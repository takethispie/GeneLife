namespace Genelife.Components.Life;

/// <summary>
/// Long-term condition in [0, 100]. Zero is a mortality trigger, not immediate
/// death; <see cref="Systems.MortalitySystem"/> guarantees death at the next check.
/// </summary>
public record struct Health
{
    public float Value;
    public float DeclineRate;
    public long LastCareTick;
    public int ChronicConditions;

    public Health(float value = 100f)
    {
        Value = Math.Clamp(value, 0f, 100f);
        DeclineRate = 0f;
        LastCareTick = 0;
        ChronicConditions = 0;
    }

}

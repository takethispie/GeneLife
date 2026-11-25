namespace Genelife.Components.Gen;

/// <summary>
/// Derived per-sim multiplier set written by the housing modifier system.
/// Neutral defaults mean a sim with no home or no modifier behaves exactly as
/// before the housing feature.
/// </summary>
public record struct NeedsModifier
{
    public float HungerDecay;
    public float EnergyDecay;
    public float HygieneDecay;
    public float BladderDecay;
    public float SleepRecovery;
    public float HygieneRecovery;
    public float HealthDrift;
    public bool NoUtilities;
    public bool Overcrowded;
    public bool IsHomeless;

    public static NeedsModifier Neutral => new()
    {
        HungerDecay = 1f,
        EnergyDecay = 1f,
        HygieneDecay = 1f,
        BladderDecay = 1f,
        SleepRecovery = 1f,
        HygieneRecovery = 1f,
        HealthDrift = 0f
    };
}

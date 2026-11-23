namespace Genelife.Appearance;

/// <summary>
/// Deterministic age-band and proportion policy. Aging never mutates the
/// persisted <c>Traits</c>; it only changes the derived descriptor.
/// </summary>
public static class AgingAppearancePolicy
{
    public const int ChildMaxAge = 12;
    public const int TeenMaxAge = 17;
    public const int AdultMaxAge = 39;
    public const int MiddleMaxAge = 59;

    public static AgeBand BandFor(int age) => age switch
    {
        <= ChildMaxAge => AgeBand.Child,
        <= TeenMaxAge => AgeBand.Teen,
        <= AdultMaxAge => AgeBand.Adult,
        <= MiddleMaxAge => AgeBand.Middle,
        _ => AgeBand.Senior
    };

    public static int HeightOffset(AgeBand band) => band switch
    {
        AgeBand.Child => -32,
        AgeBand.Teen => -8,
        AgeBand.Adult => 0,
        AgeBand.Middle => -2,
        AgeBand.Senior => -10,
        _ => 0
    };
}

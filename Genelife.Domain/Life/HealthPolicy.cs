namespace Genelife.Life;

/// <summary>
/// Domain health rules shared by simulation systems (application layer) and
/// domain factories such as <c>NewbornFactory</c>.
/// </summary>
public static class HealthPolicy
{
    /// <summary>Starting health value for a genos of the given age.</summary>
    public static float InitialForAge(int age)
    {
        var value = 100f;
        if (age > 45)
            value -= (age - 45) * 0.3f;
        if (age > 65)
            value -= (age - 65) * 0.8f;
        if (age > 80)
            value -= (age - 80) * 1.2f;
        return Math.Clamp(value, 25f, 100f);
    }
}

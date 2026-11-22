using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Housing;

/// <summary>
/// A residential building. Units are separate entities linked back to the
/// property. An owner of <see cref="Entity.Null"/> is the municipality.
/// </summary>
public record struct Property
{
    public string Name;
    public string District;
    public Entity Owner;
    public int UnitCount;
    public float Condition;
    public float NeighbourhoodScore;
    public Money MonthlyUpkeep;
    public bool IsShelter;

    public Property(
        string name,
        string district,
        Entity owner,
        int unitCount,
        float condition = 0.8f,
        float neighbourhoodScore = 0.5f,
        Money monthlyUpkeep = default,
        bool isShelter = false)
    {
        Name = name;
        District = district;
        Owner = owner == default ? Entity.Null : owner;
        UnitCount = unitCount;
        Condition = Math.Clamp(condition, 0.1f, 1f);
        NeighbourhoodScore = Math.Clamp(neighbourhoodScore, 0f, 1f);
        MonthlyUpkeep = monthlyUpkeep;
        IsShelter = isShelter;
    }
}

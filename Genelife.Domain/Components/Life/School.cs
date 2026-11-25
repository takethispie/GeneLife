using Arch.Core;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Components.Life;

/// <summary>
/// A school entity. An owner of <see cref="Entity.Null"/> is the municipality;
/// a company-owned school may charge tuition.
/// </summary>
public record struct School
{
    public string Name;
    public string District;
    public int Capacity;
    public float Quality;
    public Skill Emphasis;
    public Money MonthlyCost;
    public Entity Owner;
    public int Enrolled;

    public School(
        string name,
        string district,
        int capacity,
        float quality,
        Skill emphasis = Skill.None,
        Money monthlyCost = default,
        Entity owner = default)
    {
        Name = name;
        District = district;
        Capacity = Math.Max(1, capacity);
        Quality = Math.Clamp(quality, 0f, 1f);
        Emphasis = emphasis;
        MonthlyCost = monthlyCost;
        Owner = owner == default ? Entity.Null : owner;
        Enrolled = 0;
    }

    public bool HasCapacity => Enrolled < Capacity;
}

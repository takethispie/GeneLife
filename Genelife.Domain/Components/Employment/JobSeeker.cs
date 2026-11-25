using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a Genos looking for work. Skills are stored as a
/// bitmask of <see cref="Skill"/> tokens so the component has no list field.
/// </summary>
public record struct JobSeeker
{
    public bool IsLookingForWork;
    public Skill Skills;
    public Money DesiredSalary;

    public JobSeeker(bool isLookingForWork = true, Skill skills = Skill.None, Money desiredSalary = default)
    {
        IsLookingForWork = isLookingForWork;
        Skills = skills;
        DesiredSalary = desiredSalary;
    }

    public JobSeeker(bool isLookingForWork, Skill skills, decimal desiredSalaryUnits)
        : this(isLookingForWork, skills, Money.FromUnits(desiredSalaryUnits))
    {
    }
}

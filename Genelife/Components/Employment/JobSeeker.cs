using Genelife.Enums;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a Sim looking for work. Skills are stored as a
/// bitmask of <see cref="Skill"/> tokens so the component has no list field.
/// </summary>
public record struct JobSeeker
{
    public bool IsLookingForWork;
    public Skill Skills;
    public float DesiredSalary;

    public JobSeeker(bool isLookingForWork = true, Skill skills = Skill.None, float desiredSalary = 1000f)
    {
        IsLookingForWork = isLookingForWork;
        Skills = skills;
        DesiredSalary = desiredSalary;
    }
}

using Arch.Core;
using Genelife.Enums;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a job posting from a company.
/// Open seats are represented by separate <see cref="OpenPosition"/> entities.
/// Requirements are stored as a bitmask of <see cref="Skill"/> tokens so the
/// component has no list field.
/// </summary>
public record struct JobPosting
{
    public Entity Company;
    public Entity Office;
    public string Position;
    public float Salary;
    public Skill Requirements;

    public JobPosting(Entity company, Entity office, string position, float salary, Skill requirements = Skill.None)
    {
        Company = company;
        Office = office;
        Position = position;
        Salary = salary;
        Requirements = requirements;
    }
}

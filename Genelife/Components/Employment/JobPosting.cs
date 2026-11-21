using Arch.Core;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a job posting from a company.
/// Open seats are represented by separate <see cref="OpenPosition"/> entities.
/// </summary>
public record struct JobPosting
{
    public Entity Company;
    public Entity Office;
    public string Position;
    public float Salary;
    public List<string> Requirements;

    public JobPosting(Entity company, Entity office, string position, float salary, List<string>? requirements = null)
    {
        Company = company;
        Office = office;
        Position = position;
        Salary = salary;
        Requirements = requirements ?? new List<string>();
    }
}

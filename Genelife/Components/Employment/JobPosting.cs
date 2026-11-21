using Arch.Core;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a job posting from a company
/// </summary>
public record struct JobPosting
{
    public Entity Company;
    public Entity Office;
    public string Position;
    public float Salary;
    public int OpenPositions;
    public List<string> Requirements;

    public JobPosting(Entity company, Entity office, string position, float salary, int openPositions, List<string>? requirements = null)
    {
        Company = company;
        Office = office;
        Position = position;
        Salary = salary;
        OpenPositions = openPositions;
        Requirements = requirements ?? new List<string>();
    }
}

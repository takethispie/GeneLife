using Arch.Core;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a job posting from a company. Open seats are
/// represented by separate <see cref="OpenPosition"/> entities; OpenSeats is a
/// cached count maintained during apply phases.
/// </summary>
public record struct JobPosting
{
    public Entity Company;
    public Entity Office;
    public string Position;
    public Money SalaryPerCycle;
    public Skill Requirements;
    public int OpenSeats;
    public int TotalSeats;
    public long PostedAtTick;
    public bool IsClosed;

    public JobPosting(
        Entity company,
        Entity office,
        string position,
        Money salaryPerCycle,
        Skill requirements = Skill.None,
        int openSeats = 1,
        int totalSeats = 1,
        long postedAtTick = 0,
        bool isClosed = false)
    {
        Company = company;
        Office = office;
        Position = position;
        SalaryPerCycle = salaryPerCycle;
        Requirements = requirements;
        OpenSeats = openSeats;
        TotalSeats = totalSeats;
        PostedAtTick = postedAtTick;
        IsClosed = isClosed;
    }

    public JobPosting(Entity company, Entity office, string position, decimal salaryUnits)
        : this(company, office, position, Money.FromUnits(salaryUnits))
    {
    }
}

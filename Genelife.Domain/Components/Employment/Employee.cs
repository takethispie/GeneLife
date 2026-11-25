using Arch.Core;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing an employee relationship with a company. Salary is
/// gross pay per pay cycle (one quarter by default).
/// </summary>
public record struct Employee
{
    public Entity Company;
    public Entity Office;
    public string Position;
    public Money SalaryPerCycle;
    public Seniority Seniority;
    public long HiredAtTick;

    public Employee(Entity company, Entity office, string position, Money salaryPerCycle)
    {
        Company = company;
        Office = office;
        Position = position;
        SalaryPerCycle = salaryPerCycle;
        Seniority = Seniority.Associate;
        HiredAtTick = 0;
    }

    public Employee(Entity company, Entity office, string position, decimal salaryUnits)
        : this(company, office, position, Money.FromUnits(salaryUnits))
    {
    }
}

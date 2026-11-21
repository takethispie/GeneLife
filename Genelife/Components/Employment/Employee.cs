using Arch.Core;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing an employee relationship with a company
/// </summary>
public record struct Employee
{
    public Entity Company;
    public Entity Office;
    public string Position;
    public float Salary;

    public Employee(Entity company, Entity office, string position, float salary)
    {
        Company = company;
        Office = office;
        Position = position;
        Salary = salary;
    }
}

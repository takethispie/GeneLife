using Arch.Core;

namespace Genelife.Components.Employment;

/// <summary>
/// Derived index of employment relationships, maintained by HiringSystem.
/// This is a plain reference type (NOT an ECS component) shared between the
/// HiringSystem (writer) and repositories (readers) to avoid scanning the
/// Employee archetype.
/// </summary>
public sealed class EmploymentIndex
{
    /// <summary>Company entity -> list of employee entities.</summary>
    public Dictionary<Entity, List<Entity>> EmployeesByCompany { get; } = new();

    /// <summary>Employee entity -> company entity.</summary>
    public Dictionary<Entity, Entity> CompanyByEmployee { get; } = new();

    public void Add(Entity company, Entity employee)
    {
        if (!EmployeesByCompany.TryGetValue(company, out var list))
        {
            list = new List<Entity>();
            EmployeesByCompany[company] = list;
        }
        list.Add(employee);
        CompanyByEmployee[employee] = company;
    }

    public void Remove(Entity employee)
    {
        if (CompanyByEmployee.TryGetValue(employee, out var company))
        {
            if (EmployeesByCompany.TryGetValue(company, out var list))
            {
                list.Remove(employee);
            }
            CompanyByEmployee.Remove(employee);
        }
    }

    public int CountEmployees(Entity company)
    {
        return EmployeesByCompany.TryGetValue(company, out var list) ? list.Count : 0;
    }
}
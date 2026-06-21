namespace Genelife.Components.Employment;

/// <summary>
/// Component representing an employee relationship with a company
/// </summary>
public record struct Employee
{
    public int CompanyEntityId;
    public int OfficeEntityId;
    public string Position;
    public float Salary;
    
    public Employee(int companyEntityId, int officeEntityId, string position, float salary)
    {
        CompanyEntityId = companyEntityId;
        OfficeEntityId = officeEntityId;
        Position = position;
        Salary = salary;
    }
}

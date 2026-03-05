namespace Genelife.Domain.Work;

public class Company(
    Guid Id,
    string name,
    float revenue,
    float taxRate,
    CompanyType type
)
{
    public string Name { get; init; } = name;
    public List<Employee> Employees { get; private set; } = [];
    public CompanyType Type { get; private set; } = type;
    public float AverageProductivity { get; private set; } = 1.0F;
    public float Revenue => accountingDepartment.Revenue;
    
    private readonly Accounting accountingDepartment = new(revenue, taxRate);

    public void AddEmployee(Employee employee)
    {
        if (Employees.Contains(employee)) return;
        Employees.Add(employee);
    }
    
    public void RemoveEmployee(Employee employee) 
        => Employees = Employees.Where(e => e.Id != employee.Id).ToList();

    public void CalculatePayroll()
    {
        var salaryPayments = accountingDepartment.CalculatePayroll(this);
    }

    public void CalculateProductivity()
    {
        AverageProductivity = accountingDepartment.CalculateProductivity(this);
    }
}